using System.Numerics;
using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;
using Robust.Shared.Map;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Imperial.Medieval.ChargedAttack;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Predictable per-weapon state machine. Preparation never spends
/// recovery, hand selection never completes an attack, and stamina is paid only
/// on the Preparing -> Windup transition.
/// </summary>
public sealed partial class AdvancedLightAttackSystem : EntitySystem
{
    public const float InputDelay = 0.066f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ChargedAttackSystem _charged = default!;
    [Dependency] private readonly AdvancedLightAttackNetwork _cueNetwork = default!;
    [Dependency] private readonly MeleeLockOnSystem _lockOn = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<AdvancedLightAttackAimEvent>(OnAim);
        // charged inputs enter the same predicted commitment timeline.
        SubscribeAllEvent<AdvancedChargedAttackStartEvent>(OnChargedStart);
        SubscribeAllEvent<AdvancedChargedAttackReleaseEvent>(OnChargedRelease);
        SubscribeLocalEvent<AdvancedLightAttackComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<AdvancedLightAttackComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<AdvancedLightAttackComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<AdvancedLightAttackComponent, EntityPausedEvent>(OnPaused);
        SubscribeLocalEvent<AdvancedLightAttackComponent, ComponentShutdown>(OnShutdown);
        // attach this world to the already-registered, persistent router.
        _cueNetwork.CueReceived += OnCuePacket;
    }

    public override void Shutdown()
    {
        // reconnecting must never deliver packets to a stopped world.
        _cueNetwork.CueReceived -= OnCuePacket;
        base.Shutdown();
    }

    private void OnCuePacket(MsgAdvancedLightAttackCue message)
    {
        RaiseLocalEvent(new AdvancedLightAttackCueReceivedEvent(message));
    }

    public static TimeSpan TickDuration(float seconds, ushort tickRate, TimeSpan tickPeriod)
    {
        if (!float.IsFinite(seconds) || seconds <= 0f)
            return TimeSpan.Zero;
        return TimeSpan.FromTicks((long)MathF.Ceiling(seconds * tickRate) * tickPeriod.Ticks);
    }

    public float GetStaminaCost(AdvancedLightAttackComponent component)
    {
        var cost = component.StaminaCost ?? _prototypes.Index(component.Preset).StaminaCost;
        return float.IsFinite(cost) ? MathF.Max(0f, cost) : 0f;
    }

    /// <summary>
    /// all advanced LIGHT arc selection lives here. The melee system only
    /// exposes its existing physical/historical ArcRayCast as a small hook.
    /// Called synchronously during the strike, inside the server's history scope.
    /// </summary>
    public EntityUid? SelectLightRaycastTarget(EntityUid user, EntityUid weapon,
        MeleeWeaponComponent melee, EntityCoordinates aim, AdvancedLightAttackComponent advanced)
    {
        var mapAim = _transform.ToMapCoordinates(aim);
        var userMap = _transform.GetMapCoordinates(user);
        if (mapAim.MapId != userMap.MapId)
            return null;

        var direction = mapAim.Position - userMap.Position;
        if (direction.LengthSquared() < 0.000001f)
            return null;

        var angle = advanced.RaycastArc ?? _prototypes.Index(advanced.Preset).RaycastArc;
        angle = float.IsFinite(angle) ? Math.Clamp(angle, 0f, 360f) : 30f;
        var hits = _melee.LightAttackArcRaycast(userMap.Position, direction.ToWorldAngle(),
            Angle.FromDegrees(angle), melee.Range, userMap.MapId, user);

        // An existing lock wins only when present in the unobstructed arc;
        // an out-of-range or blocked lock makes the attack miss, not retarget.
        if (_lockOn.GetTarget(user) is { } locked)
            return hits.Contains(locked) && HasComp<DamageableComponent>(locked) ? locked : null;

        var candidates = new List<(EntityUid Id, float Distance)>();
        foreach (var hit in hits)
        {
            if (hit == user || TerminatingOrDeleted(hit) || !HasComp<DamageableComponent>(hit) ||
                !_melee.TryGetLightAttackTargetPosition(user, hit, out var position))
                continue;
            candidates.Add((hit, Vector2.Distance(userMap.Position, position)));
        }
        if (candidates.Count == 0)
            return null;

        var nearest = candidates.Min(c => c.Distance);
        var equallyClose = candidates
        .Where(c => c.Distance - nearest <= 0.01f)
        .Select(c => c.Id)
        .OrderBy(id => GetNetEntity(id).Id)
        .ToList();

        if (equallyClose.Count == 1)
            return equallyClose[0];

        // The same swing produces the same roll during prediction and replay.
        var seed = unchecked(((int)advanced.AttackId * 397 ^ GetNetEntity(weapon).Id) * 397 ^ GetNetEntity(user).Id);

        var random = new System.Random(seed);
        return equallyClose[random.Next(equallyClose.Count)];
    }

    public float GetWindup(AdvancedLightAttackComponent component)
    {
        var windup = component.Windup ?? _prototypes.Index(component.Preset).Windup;
        return float.IsFinite(windup) ? MathF.Max(0f, windup) : 0f;
    }

    public EntProtoId GetEffect(AdvancedLightAttackComponent component)
        => component.WindupEffect ?? _prototypes.Index(component.Preset).WindupEffect;

    public float GetChargeWindup(AdvancedLightAttackComponent component)
    {
        var windup = component.ChargeWindup ?? _prototypes.Index(component.Preset).ChargeWindup;
        return float.IsFinite(windup) ? MathF.Max(0f, windup) : 0.6f;
    }

    public EntProtoId GetCueEffect(EntityUid weapon, AdvancedLightAttackComponent component)
        => component.IsCharged && TryComp<ChargedAttackComponent>(weapon, out var charged)
            ? component.ChargeWindupEffect ?? charged.EffectProtoId
            : GetEffect(component);

    /// <summary>Only the charge bonus changes; base health/poise damage is retained.</summary>
    public static (float Damage, float Poise) GetChargeFractions(TimeSpan strike, TimeSpan minimum, TimeSpan maximum)
    {
        var progress = maximum <= minimum ? 1f :
            Math.Clamp((float)((strike - minimum).TotalSeconds / (maximum - minimum).TotalSeconds), 0f, 1f);
        return (0.1f + 0.9f * progress, 0.5f + 0.5f * progress);
    }

    private void OnChargedStart(AdvancedChargedAttackStartEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !_melee.TryGetWeapon(user, out var weapon, out var melee) ||
            !TryGetEntity(message.Weapon, out var requested) || requested != weapon)
            return;
        TryBeginCharged(user, weapon, melee, message.Coordinates, message.AttackId);
    }

    private void OnChargedRelease(AdvancedChargedAttackReleaseEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user)
            TryReleaseCharge(user, message);
    }

    public bool TryBeginCharged(EntityUid user, EntityUid weapon, MeleeWeaponComponent melee,
        Robust.Shared.Map.NetCoordinates coordinates, uint attackId = 0)
        => HasComp<ChargedAttackComponent>(weapon) &&
           TryBegin(user, weapon, melee, new LightAttackEvent(null, GetNetEntity(weapon), coordinates, attackId), charged: true);

    public bool TryReleaseCharge(EntityUid user, AdvancedChargedAttackReleaseEvent message)
    {
        if (!TryGetEntity(message.Weapon, out var weapon) ||
            !TryComp<AdvancedLightAttackComponent>(weapon, out var component) ||
            component.Attacker != user || !component.IsCharged || component.AttackId != message.AttackId ||
            !_melee.IsAdvancedWeaponHeldOrSelected(user, weapon.Value))
            return false;

        if (!component.SecondaryHeld)
            return true;
        component.SecondaryHeld = false;
        // Releasing after a strike/cancellation only clears its input latch.
        if (IsPending(weapon.Value))
        {
            // Release publishes its exact aim and revised strike time together.
            TryUpdateAim(user, new AdvancedLightAttackAimEvent(message.Weapon, message.AttackId, null, message.Coordinates), publish: false);
            component.Strikes = _timing.CurTime < component.MinimumStrike ? component.MinimumStrike :
                _timing.CurTime > component.MaximumStrike ? component.MaximumStrike : _timing.CurTime;
            Publish(weapon.Value, component);
        }
        else
            Dirty(weapon.Value, component);
        return true;
    }

    public bool IsPending(EntityUid weapon)
        => TryComp<AdvancedLightAttackComponent>(weapon, out var component) &&
           component.Phase is AdvancedLightAttackPhase.Preparing or AdvancedLightAttackPhase.Windup;

    public bool IsAttackLocked(EntityUid user)
        => TryComp<AdvancedLightAttackUserComponent>(user, out var component) &&
           component.AttackLockedUntil > _timing.CurTime;

    /// <summary>Always derive this flag from distinct real weapons, never virtual second-hand grips.</summary>
    public bool RefreshDualWielding(EntityUid user)
    {
        var count = 0;
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!HasComp<VirtualItemComponent>(held) &&
                TryComp<MeleeWeaponComponent>(held, out var melee) && !melee.MustBeEquippedToUse)
                count++;
        }

        var component = EnsureComp<AdvancedLightAttackUserComponent>(user);
        var dual = count >= 2;
        if (component.IsDualWielding != dual)
        {
            component.IsDualWielding = dual;
            Dirty(user, component);
        }
        return dual;
    }

    public bool TryBegin(EntityUid user, EntityUid weapon, MeleeWeaponComponent melee, LightAttackEvent attack, bool charged = false)
    {
        if (!TryComp<AdvancedLightAttackComponent>(weapon, out var component) || IsPending(weapon) ||
            melee.NextAttack > _timing.CurTime || IsAttackLocked(user) ||
            !CanContinue(user, weapon, melee) ||
            !_melee.CheckAdvancedLightAttackAttempt(user, weapon))
            return false;

        var rate = _melee.GetAttackRate(weapon, user, melee);
        if (!float.IsFinite(rate) || rate <= 0f ||
            !TryGetEntity(attack.Coordinates.NetEntity, out _))
            return false;
        var coordinates = GetCoordinates(attack.Coordinates);
        if (!coordinates.IsValid(EntityManager))
            return false;
        var map = _transform.ToMapCoordinates(coordinates);
        if (!float.IsFinite(map.Position.X) || !float.IsFinite(map.Position.Y) || map.MapId != Transform(user).MapID)
            return false;

        // Repeated requests for the same weapon cannot restart a wind-up. Input
        // IDs are monotonic for a given wielder, and never choose server timing.
        if (component.Attacker == user && attack.AttackId != 0 && attack.AttackId <= component.AttackId)
            return false;

        var id = attack.AttackId == 0 ? component.AttackId + 1 : attack.AttackId;
        if (id == 0)
            id = 1;
        component.AttackId = id;
        component.Attacker = user;
        component.PendingAttack = new LightAttackEvent(attack.Target, attack.Weapon, attack.Coordinates, id);
        component.IsCharged = charged;
        component.SecondaryHeld = charged;
        component.Phase = AdvancedLightAttackPhase.Preparing;
        component.PrimaryHeld = !charged;
        component.UsesPrimaryLatch = !charged;
        component.Started = _timing.CurTime;
        component.CueStarts = component.Started + TickDuration(InputDelay, _timing.TickRate, _timing.TickPeriod);
        var minimum = charged ? GetChargeWindup(component) : GetWindup(component);
        var maximum = minimum;
        if (charged && TryComp<ChargedAttackComponent>(weapon, out var charge))
            maximum = float.IsFinite(charge.MaxAttackTime) ? MathF.Max(minimum, charge.MaxAttackTime) : minimum;
        component.MinimumStrike = component.CueStarts + TickDuration(minimum, _timing.TickRate, _timing.TickPeriod);
        component.MaximumStrike = component.CueStarts + TickDuration(maximum, _timing.TickRate, _timing.TickPeriod);
        component.Strikes = charged ? component.MaximumStrike : component.MinimumStrike;
        component.PresentationState = AdvancedMeleeVisualState.Scheduled;
        component.Presentation = null;
        component.PresentationStrike = component.Strikes;
        RefreshDualWielding(user);
        Publish(weapon, component);
        return true;
    }

    private bool CanContinue(EntityUid user, EntityUid weapon, MeleeWeaponComponent melee)
        => !TerminatingOrDeleted(user) && !Paused(user) && !Paused(weapon) &&
           _combat.IsInCombatMode(user) && _melee.IsAdvancedWeaponHeldOrSelected(user, weapon) &&
           _blocker.CanAttack(user, weapon: (weapon, melee)) &&
           (!(TryComp<ChargedAttackComponent>(weapon, out var charge) && charge.CurrentAttacking) ||
            TryComp<AdvancedLightAttackComponent>(weapon, out var advanced) && advanced.IsCharged && IsPending(weapon));

    public void ReleasePrimary(EntityUid weapon)
    {
        if (!TryComp<AdvancedLightAttackComponent>(weapon, out var component) ||
            !component.PrimaryHeld && !component.UsesPrimaryLatch)
            return;
        component.PrimaryHeld = false;
        component.UsesPrimaryLatch = false;
        Dirty(weapon, component);
    }

    private void OnAim(AdvancedLightAttackAimEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user)
            TryUpdateAim(user, message);
    }

    public bool TryUpdateAim(EntityUid user, AdvancedLightAttackAimEvent message)
        => TryUpdateAim(user, message, publish: true);

    private bool TryUpdateAim(EntityUid user, AdvancedLightAttackAimEvent message, bool publish)
    {
        if (!TryGetEntity(message.Weapon, out var weapon) ||
            !TryComp<AdvancedLightAttackComponent>(weapon, out var component) ||
            component.Attacker != user || component.PendingAttack is not { } pending ||
            component.AttackId != message.AttackId || !IsPending(weapon.Value) ||
            !_melee.IsAdvancedWeaponHeldOrSelected(user, weapon.Value) ||
            !TryComp<MeleeWeaponComponent>(weapon, out var melee) ||
            !TryGetEntity(message.Coordinates.NetEntity, out _))
            return false;

        var coordinates = GetCoordinates(message.Coordinates);
        if (!coordinates.IsValid(EntityManager))
            return false;
        var map = _transform.ToMapCoordinates(coordinates);
        if (!float.IsFinite(map.Position.X) || !float.IsFinite(map.Position.Y) ||
            map.MapId != Transform(user).MapID)
            return false;

        EntityUid? target = null;
        if (message.Target is { } netTarget && TryGetEntity(netTarget, out var resolved) &&
            !TerminatingOrDeleted(resolved) && resolved != weapon &&
            Vector2.DistanceSquared(map.Position, _transform.GetWorldPosition(user)) <= melee.Range * melee.Range)
            target = resolved;

        var netTargetValue = GetNetEntity(target);
        if (pending.Target == netTargetValue && pending.Coordinates.Equals(message.Coordinates))
            return true;

        component.PendingAttack = new LightAttackEvent(netTargetValue, message.Weapon, message.Coordinates, component.AttackId);
        if (publish)
            Publish(weapon.Value, component);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var users = EntityQueryEnumerator<AdvancedLightAttackUserComponent>();
        while (users.MoveNext(out var user, out _))
        {
            if (_net.IsServer || _players.LocalEntity == user)
                RefreshDualWielding(user);
        }
        var query = EntityQueryEnumerator<AdvancedLightAttackComponent, MeleeWeaponComponent>();
        while (query.MoveNext(out var weapon, out var component, out var melee))
        {
            if (component.Attacker is not { } user || _net.IsClient && _players.LocalEntity != user)
                continue;

            if (component.Phase == AdvancedLightAttackPhase.Recovery)
            {
                if (_timing.CurTime >= melee.NextAttack)
                {
                    component.Phase = AdvancedLightAttackPhase.Idle;
                    Dirty(weapon, component);
                }
                continue;
            }

            if (!IsPending(weapon))
                continue;
            if (!CanContinue(user, weapon, melee) || IsAttackLocked(user) || melee.NextAttack > _timing.CurTime)
            {
                Cancel(weapon, component);
                continue;
            }

            if (component.Phase == AdvancedLightAttackPhase.Preparing && _timing.CurTime >= component.CueStarts)
            {
                var rate = _melee.GetAttackRate(weapon, user, melee);
                if (!float.IsFinite(rate) || rate <= 0f || !_melee.CheckAdvancedLightAttackAttempt(user, weapon))
                {
                    Cancel(weapon, component);
                    continue;
                }
                var cost = component.IsCharged && TryComp<ChargedAttackComponent>(weapon, out var charged)
                    ? MathF.Max(0f, charged.StaminaDamage) : GetStaminaCost(component);
                if (!float.IsFinite(cost))
                {
                    Cancel(weapon, component);
                    continue;
                }
                // Exertion ignores armor and has no weapon origin: incoming-hit
                // poise/parry handlers must not cancel the attacker's own cost.
                if (cost > 0f && !_stamina.TryTakeStamina(user, cost, ignoreResist: true, log: false))
                {
                    Cancel(weapon, component);
                    continue;
                }

                component.Phase = AdvancedLightAttackPhase.Windup;
                if (component.IsCharged)
                    _charged.BeginAdvancedWindup(weapon, user, component.CueStarts);
                Publish(weapon, component);
            }

            if (component.Phase == AdvancedLightAttackPhase.Windup && _timing.CurTime >= component.Strikes &&
                component.PendingAttack is { } attack)
            {
                if (component.IsCharged)
                    _melee.CompleteAdvancedChargedAttack(user, weapon, melee, attack);
                else
                    _melee.CompleteAdvancedLightAttack(user, weapon, melee, attack);
            }
        }
    }

    /// <summary>The only route from a committed wind-up to a normal melee strike.</summary>
    public bool TryConsumeStrike(EntityUid user, EntityUid weapon, LightAttackEvent attack,
        [NotNullWhen(true)] out LightAttackEvent? accepted, bool charged = false)
    {
        accepted = null;
        if (!TryComp<AdvancedLightAttackComponent>(weapon, out var component) ||
            component.Attacker != user || component.IsCharged != charged || component.Phase != AdvancedLightAttackPhase.Windup ||
            component.PendingAttack == null || component.AttackId != attack.AttackId ||
            component.PendingAttack.Weapon != attack.Weapon || attack.Weapon != GetNetEntity(weapon) ||
            _timing.CurTime < component.Strikes || IsAttackLocked(user) ||
            !TryComp<MeleeWeaponComponent>(weapon, out var melee) || melee.NextAttack > _timing.CurTime ||
            !CanContinue(user, weapon, melee))
            return false;

        accepted = component.PendingAttack;
        CaptureScheduledPresentation(weapon, component, accepted.Coordinates);
        component.PendingAttack = null;
        component.Phase = AdvancedLightAttackPhase.Recovery;
        component.UsesPrimaryLatch = !charged;
        Publish(weapon, component);
        return true;
    }

    public void Cancel(EntityUid weapon, AdvancedLightAttackComponent component)
    {
        if (component.Phase is not (AdvancedLightAttackPhase.Preparing or AdvancedLightAttackPhase.Windup))
            return;
        component.PendingAttack = null;
        component.PresentationState = AdvancedMeleeVisualState.Cancelled;
        component.Phase = AdvancedLightAttackPhase.Idle;
        if (component.IsCharged && component.Attacker is { } user &&
            TryComp<ChargedAttackComponent>(weapon, out var charged) && charged.CurrentAttacking)
            _charged.StopAttacking(weapon, charged, user);
        // Keep the input latch and weapon recovery: cancellation is neither a
        // fresh press nor a way to clear a cooldown. Stamina is never refunded.
        Publish(weapon, component);
    }

    public void StartParryLockout(EntityUid user, TimeSpan until)
    {
        var wielder = EnsureComp<AdvancedLightAttackUserComponent>(user);
        if (until > wielder.AttackLockedUntil)
            wielder.AttackLockedUntil = until;
        Dirty(user, wielder);

        // Ownership, rather than the currently selected hand, defines every
        // pending commitment that a parry interrupts (including unarmed attacks).
        var pending = EntityQueryEnumerator<AdvancedLightAttackComponent>();
        while (pending.MoveNext(out var weapon, out var attack))
        {
            if (attack.Attacker == user)
                Cancel(weapon, attack);
        }

        foreach (var weapon in _hands.EnumerateHeld(user))
        {
            if (TryComp<ChargedAttackComponent>(weapon, out var charge) && charge.CurrentAttacking)
                _charged.StopAttacking(weapon, charge, user);
        }
    }

    public void EndParryLockout(EntityUid user)
    {
        if (!TryComp<AdvancedLightAttackUserComponent>(user, out var component))
            return;
        component.AttackLockedUntil = TimeSpan.Zero;
        Dirty(user, component);
    }

    private void Publish(EntityUid weapon, AdvancedLightAttackComponent component)
    {
        UpdateScheduledPresentation(weapon, component);
        component.Revision++;
        if (_net.IsServer)
        {
            component.ServerRealSample = _timing.RealTime;
            component.ServerSimulationSample = _timing.CurTime;
        }
        Dirty(weapon, component);
        if (!_net.IsServer || component.Attacker is not { } user || TerminatingOrDeleted(user))
            return;

        var message = new MsgAdvancedLightAttackCue
        {
            Weapon = GetNetEntity(weapon),
            Attacker = GetNetEntity(user),
            AttackId = component.AttackId,
            Revision = component.Revision,
            Phase = component.Phase,
            Started = component.Started,
            CueStarts = component.CueStarts,
            Strikes = component.Strikes,
            ServerRealSample = component.ServerRealSample,
            ServerSimulationSample = component.ServerSimulationSample,
            Effect = GetCueEffect(weapon, component).Id,
            PresentationState = component.PresentationState,
            PresentationStrike = component.PresentationStrike,
            Presentation = component.Presentation,
        };
        foreach (var session in Filter.PvsExcept(user).Recipients)
            _net.ServerSendMessage(message, session.Channel);
    }

    private void OnDropped(EntityUid uid, AdvancedLightAttackComponent component, DroppedEvent args)
    {
        Cancel(uid, component);
        RefreshDualWielding(args.User);
    }

    private void OnUnequipped(EntityUid uid, AdvancedLightAttackComponent component, GotUnequippedHandEvent args)
    {
        Cancel(uid, component);
        ReleasePrimary(uid);
        RefreshDualWielding(args.User);
    }

    private void OnEquipped(EntityUid uid, AdvancedLightAttackComponent component, GotEquippedHandEvent args)
        => RefreshDualWielding(args.User);

    private void OnPaused(EntityUid uid, AdvancedLightAttackComponent component, ref EntityPausedEvent args)
        => Cancel(uid, component);

    private void OnShutdown(EntityUid uid, AdvancedLightAttackComponent component, ComponentShutdown args)
        => Cancel(uid, component);
}
