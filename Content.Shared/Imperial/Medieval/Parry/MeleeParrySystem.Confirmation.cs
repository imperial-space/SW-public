using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.MeleeParry.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Timing;

namespace Content.Shared.MeleeParry;

public sealed partial class MeleeParrySystem
{
    private const float MaximumConfirmationSeconds = 0.1f;
    private float _confirmationSeconds;
    private readonly Dictionary<EntityUid, List<ParryAttempt>> _confirmationAttempts = new();
    private readonly List<EntityUid> _confirmationRemove = new();
    private ConfirmedHitContext? _confirmedHit;

    private sealed class ParryAttempt
    {
        public readonly EntityUid Weapon;
        public readonly TimeSpan Starts;
        public readonly float Window;
        public TimeSpan? Superseded;

        public readonly HashSet<(EntityUid Attacker, EntityUid Weapon, TimeSpan Impact)> Hits = new();

        public ParryAttempt(EntityUid weapon, TimeSpan starts, float window)
        {
            Weapon = weapon;
            Starts = starts;
            Window = window;
        }
    }

    /// <summary>One bounded, tick-aligned delivery allowance. It never widens the active parry window.</summary>
    public TimeSpan ConfirmationBuffer => TimeSpan.FromTicks(ConfirmationTicks * _timing.TickPeriod.Ticks);

    private long ConfirmationTicks => (long)MathF.Ceiling(_confirmationSeconds * _timing.TickRate);

    private void InitializeConfirmation()
    {
        _cfg.OnValueChanged(CCVars.ParryConfirmationBuffer, value =>
            _confirmationSeconds = float.IsFinite(value) ? Math.Clamp(value, 0f, MaximumConfirmationSeconds) : 0f, true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnConfirmationRoundRestart);
    }

    private void OnConfirmationRoundRestart(RoundRestartCleanupEvent args)
    {
        _confirmationAttempts.Clear();
        _confirmedHit = null;
    }

    public override void Shutdown()
    {
        _confirmationAttempts.Clear();
        _confirmedHit = null;
        base.Shutdown();
    }

    private bool TryGetParryInputTime(MeleeParryStorageComponent storage, GameTick tick, out TimeSpan time)
    {
        time = _timing.CurTime;
        if (tick == GameTick.Zero || tick <= storage.LastParryInputTick)
            return false;

        if (!_netMan.IsServer)
            return true;

        // Predictive inputs use the same tick timeline as the engine's event queue.
        // A future tick, replay, or input older than the delivery allowance is rejected.
        if (tick > _timing.CurTick)
            return false;

        var age = _timing.CurTick.Value - tick.Value;
        if (age > ConfirmationTicks)
            return false;

        time -= TimeSpan.FromTicks((long)age * _timing.TickPeriod.Ticks);
        return time + TimeSpan.FromSeconds(_desyncTolerance) >= storage.NextParryTime;
    }

    private void RecordConfirmationAttempt(EntityUid user, EntityUid weapon, MeleeParryComponent parry, TimeSpan starts)
    {
        if (!_netMan.IsServer)
            return;

        var window = parry.ParryWindow;
        if (TryComp<MedievalWeaponSkillCategoryComponent>(weapon, out var category))
            window *= category.Skill.GetParryData().Window;

        if (!_confirmationAttempts.TryGetValue(user, out var attempts))
            _confirmationAttempts[user] = attempts = new();

        if (attempts.Count != 0)
            attempts[^1].Superseded = starts;

        attempts.Add(new ParryAttempt(weapon, starts, window));
    }

    /// <summary>Snapshot the attacking weapon's existing parryability formula at contact.</summary>
    public float GetConfirmationParryDamage(EntityUid weapon)
    {
        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee) ||
            !melee.Damage.DamageDict.TryGetValue("ParryAble", out var value))
        {
            return 0f;
        }

        var damage = (float)value;
        if (TryComp<MedievalWeaponSkillCategoryComponent>(weapon, out var category))
            damage *= category.Skill.GetParryData().Able;

        return float.IsFinite(damage) ? MathF.Max(0f, damage) : 0f;
    }

    /// <summary>
    /// Resolve only against an attempt that was active at the original contact and
    /// used the weapon held at that contact. A later press cannot protect an older hit.
    /// </summary>
    public bool TryConfirmMeleeParry(EntityUid user, EntityUid attacker, EntityUid attackingWeapon,
        EntityUid defendingWeapon, float parryDamage, TimeSpan impact)
    {
        if (!_netMan.IsServer || parryDamage <= 0f ||
            !_confirmationAttempts.TryGetValue(user, out var attempts) ||
            !TryComp<MeleeParryComponent>(defendingWeapon, out var parry) ||
            !TryComp<MeleeParryStorageComponent>(user, out var storage) ||
            !TryComp<MeleeWeaponComponent>(defendingWeapon, out var defenderMelee) ||
            !TryComp<MeleeWeaponComponent>(attackingWeapon, out var attackerMelee))
        {
            return false;
        }

        for (var i = attempts.Count - 1; i >= 0; i--)
        {
            var attempt = attempts[i];
            if (attempt.Weapon != defendingWeapon || impact < attempt.Starts ||
                attempt.Superseded is { } superseded && impact >= superseded ||
                impact >= attempt.Starts + TimeSpan.FromSeconds(attempt.Window * parryDamage))
            {
                continue;
            }

            // repeated callbacks reuse the result; later impacts remain eligible.
            if (!attempt.Hits.Add((attacker, attackingWeapon, impact)))
                return true;
            ApplySuccessfulParry(user, attacker, attackingWeapon, attackerMelee, defendingWeapon,
                parry, storage, defenderMelee, impact);
            return true;
        }

        return false;
    }

    // deduplicate immediate health/stamina callbacks without consuming the window.
    private bool TryRecordParryHit(EntityUid user, EntityUid weapon, EntityUid attacker,
        EntityUid attackingWeapon, TimeSpan impact)
    {
        if (!_confirmationAttempts.TryGetValue(user, out var attempts))
            return true;
        for (var i = attempts.Count - 1; i >= 0; i--)
        {
            var attempt = attempts[i];
            if (attempt.Weapon == weapon && attempt.Starts <= impact &&
                (attempt.Superseded == null || impact < attempt.Superseded.Value))
                return attempt.Hits.Add((attacker, attackingWeapon, impact));
        }
        return true;
    }

    private void PruneConfirmationHistory()
    {
        if (!_netMan.IsServer)
            return;

        _confirmationRemove.Clear();
        var earliest = _timing.CurTime - ConfirmationBuffer;
        foreach (var (user, attempts) in _confirmationAttempts)
        {
            if (TerminatingOrDeleted(user))
            {
                _confirmationRemove.Add(user);
                continue;
            }

            // keep the latest active attempt after successful catches.
            // Its duration retains the existing per-attacking-weapon multiplier.
            attempts.RemoveAll(attempt => attempt.Superseded is { } end && end < earliest);
            if (attempts.Count == 0)
                _confirmationRemove.Add(user);
        }

        foreach (var user in _confirmationRemove)
            _confirmationAttempts.Remove(user);
    }

    private sealed record ConfirmedHitContext(EntityUid Attacker, HashSet<EntityUid> Targets);

    /// <summary>Prevent live parry state from being applied a second time during confirmed damage callbacks.</summary>
    public IDisposable BeginConfirmedMeleeHit(EntityUid attacker, List<EntityUid> targets)
    {
        var previous = _confirmedHit;
        _confirmedHit = new ConfirmedHitContext(attacker, new HashSet<EntityUid>(targets));
        return new ConfirmationScope(this, previous);
    }

    private bool IsResolvingConfirmedHit(EntityUid target, EntityUid attacker)
    {
        return _confirmedHit is { } context && context.Attacker == attacker && context.Targets.Contains(target);
    }

    private sealed class ConfirmationScope(MeleeParrySystem owner, ConfirmedHitContext? previous) : IDisposable
    {
        public void Dispose() => owner._confirmedHit = previous;
    }
}
