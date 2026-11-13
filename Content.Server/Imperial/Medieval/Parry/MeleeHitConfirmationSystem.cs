using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.MeleeParry;
using Content.Shared.MeleeParry.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Parry;

/// <summary>
/// Holds validated melee consequences for a short delivery allowance. Contact,
/// attacking weapon and charge/grip poise are captured before the wait.
/// </summary>
public sealed class MeleeHitConfirmationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MeleeParrySystem _parry = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PoiseSystem _poise = default!;

    private readonly List<PendingHit> _pending = new();

    private readonly record struct Candidate(EntityUid Target, EntityUid? DefendingWeapon);
    private sealed record PendingHit(EntityUid User, EntityUid Weapon, TimeSpan Impact,
        TimeSpan Due, float ParryDamage, int PoiseDamage, List<Candidate> Targets,
        Action<List<EntityUid>, int?> Resolve);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        _pending.Clear();
        base.Shutdown();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args) => _pending.Clear();

    public bool TrySchedule(EntityUid user, EntityUid weapon, List<EntityUid> targets,
        Action<List<EntityUid>, int?> resolve)
    {
        var parryDamage = _parry.GetConfirmationParryDamage(weapon);
        if (parryDamage <= 0f || targets.Count == 0)
            return false;

        var candidates = new List<Candidate>(targets.Count);
        var canParry = false;
        foreach (var target in targets)
        {
            EntityUid? defendingWeapon = null;
            var held = _hands.GetActiveItem(target);
            if (HasComp<MeleeParryAbleComponent>(target) &&
                HasComp<MeleeParryStorageComponent>(target) && held is { } item && HasComp<MeleeParryComponent>(item))
            {
                defendingWeapon = item;
                canParry = true;
            }

            candidates.Add(new Candidate(target, defendingWeapon));
        }

        if (!canParry)
            return false;

        var now = _timing.CurTime;
        var hit = new PendingHit(user, weapon, now, now + _parry.ConfirmationBuffer,
            parryDamage, _poise.GetMeleePoiseDamage(weapon), candidates, resolve);

        if (hit.Due <= now)
            Confirm(hit);
        else
            _pending.Add(hit);

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        for (var i = 0; i < _pending.Count;)
        {
            var hit = _pending[i];
            if (_timing.CurTime < hit.Due)
            {
                i++;
                continue;
            }

            // Remove before callbacks: a secondary attack must not execute this hit twice.
            _pending.RemoveAt(i);
            Confirm(hit);
        }
    }

    private void Confirm(PendingHit hit)
    {
        if (TerminatingOrDeleted(hit.User) || TerminatingOrDeleted(hit.Weapon) || Paused(hit.User) || Paused(hit.Weapon))
            return;

        var confirmed = new List<EntityUid>(hit.Targets.Count);
        foreach (var candidate in hit.Targets)
        {
            if (TerminatingOrDeleted(candidate.Target) || Paused(candidate.Target))
                continue;

            if (candidate.DefendingWeapon is { } defending &&
                _parry.TryConfirmMeleeParry(candidate.Target, hit.User, hit.Weapon,
                    defending, hit.ParryDamage, hit.Impact))
            {
                continue;
            }

            confirmed.Add(candidate.Target);
        }

        if (confirmed.Count == 0)
            return;

        using (_parry.BeginConfirmedMeleeHit(hit.User, confirmed))
            hit.Resolve(confirmed, hit.PoiseDamage);
    }
}
