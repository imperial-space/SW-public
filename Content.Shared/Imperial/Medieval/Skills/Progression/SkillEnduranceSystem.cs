using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.Grab;
using Content.Shared.Imperial.Medieval.Grab.Components;
using Content.Shared.Imperial.Medieval.Sprint;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Skills;

[ByRefEvent]
public record struct NeedsDecayEvent(float Multiplier = 1f);

/// <summary>Keeps original control timers alive while endurance pays for their suppression.</summary>
public sealed class SkillEnduranceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    private float _elapsed;

    private float Setting(string key) => _prototypes.Index<SkillPrototype>(SharedSkillsSystem.EnduranceId).Modifiers[key];
    private bool Legendary(EntityUid uid) => TryComp<SkillsComponent>(uid, out var skills)
        && SkillScaling.Level(skills, SharedSkillsSystem.EnduranceId) >= SkillScaling.Legendary;
    private bool Protected(EntityUid uid) => Legendary(uid) && TryComp<SkillProgressionComponent>(uid, out var state)
        && state.ControlProtectionUntil > _timing.CurTime;
    public bool Converts(EntityUid uid) => Legendary(uid) && TryComp<StaminaComponent>(uid, out var stamina)
        && !stamina.Critical && stamina.CritThreshold > 0
        && stamina.StaminaDamage <= stamina.CritThreshold * (1f - Setting("ControlStaminaThreshold"));

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillsComponent, FinalizeMovementSpeedEvent>(OnMovement);
        SubscribeLocalEvent<SkillsComponent, CanActWhileStunnedEvent>(OnStunPermission);
        SubscribeLocalEvent<SkillsComponent, StunAttemptEvent>(OnStun);
        SubscribeLocalEvent<SkillsComponent, BeforeStatusEffectAddedEvent>(OnStatus);
        SubscribeLocalEvent<SkillsComponent, KnockDownAttemptEvent>(OnKnockdown);
        SubscribeLocalEvent<SkillsComponent, BeingGrabbedAttemptEvent>(OnGrab);
        SubscribeLocalEvent<SkillsComponent, NeedsDecayEvent>(OnNeeds);
        SubscribeLocalEvent<SkillsComponent, CanSprintEvent>(OnSprint);
    }

    private void OnNeeds(EntityUid uid, SkillsComponent skills, ref NeedsDecayEvent args)
    {
        if (Legendary(uid))
            args.Multiplier *= Setting("NeedsMultiplier");
    }

    private void OnSprint(EntityUid uid, SkillsComponent skills, ref CanSprintEvent args)
    {
        if (SkillScaling.Level(skills, SharedSkillsSystem.EnduranceId) < SkillScaling.Basic)
        {
            args.Cancelled = true;
            return;
        }
        if (SkillScaling.Level(skills, SharedSkillsSystem.EnduranceId) >= SkillScaling.Trained)
            return;
        if (HasComp<ActiveGrabberComponent>(uid) || TryComp<PullerComponent>(uid, out var puller) && puller.Pulling != null)
            args.Cancelled = true;
        foreach (var held in _hands.EnumerateHeld(uid))
            if (HasComp<MultiHandedItemComponent>(held))
                args.Cancelled = true;
        foreach (var slot in new[] { "outerClothing", "head" })
            if (_inventory.TryGetSlotEntity(uid, slot, out var item) && TryComp<ClothingSpeedModifierComponent>(item, out var clothing) && _toggle.IsActivated(item.Value) && clothing.SprintModifier < 0.85f)
                args.Cancelled = true;
    }

    private void OnStunPermission(EntityUid uid, SkillsComponent skills, ref CanActWhileStunnedEvent args) => args.Allowed |= Converts(uid) || Protected(uid);
    private void OnStun(EntityUid uid, SkillsComponent skills, ref StunAttemptEvent args) => args.Cancelled |= Protected(uid);
    private void OnStatus(EntityUid uid, SkillsComponent skills, ref BeforeStatusEffectAddedEvent args)
    {
        if (!Protected(uid) || !_prototypes.TryIndex(args.Effect, out var effect))
            return;
        args.Cancelled |= effect.Components.ContainsKey("StunnedStatusEffect")
            || effect.Components.ContainsKey("KnockdownStatusEffect");
    }
    private void OnGrab(EntityUid uid, SkillsComponent skills, BeingGrabbedAttemptEvent args)
    {
        if (Protected(uid))
            args.Cancel();
    }

    private void OnKnockdown(EntityUid uid, SkillsComponent skills, ref KnockDownAttemptEvent args)
    {
        if (Protected(uid))
        {
            args.Cancelled = true;
            args.BlockForced = true;
            return;
        }
        if (args.Cancelled || args.Time == null || !Converts(uid))
            return;
        var state = EnsureComp<SkillProgressionComponent>(uid);
        var end = _timing.CurTime + args.Time.Value;
        if (end > state.ConvertedKnockdownUntil)
            state.ConvertedKnockdownUntil = end;
        args.Cancelled = true;
        args.BlockForced = true;
        Dirty(uid, state);
    }

    private void OnMovement(EntityUid uid, SkillsComponent skills, ref FinalizeMovementSpeedEvent args)
    {
        if (!Legendary(uid))
            return;
        var state = EnsureComp<SkillProgressionComponent>(uid);
        state.ConvertedSlowdown = 1f - Math.Min(args.Modifiers.ConvertibleWalkSlowdown, args.Modifiers.ConvertibleSprintSlowdown);
        if (!Converts(uid) && !Protected(uid))
            return;
        args.Modifiers.RemoveConvertibleSlowdowns();
        if (Protected(uid))
            return;
        if (HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid) || state.ConvertedKnockdownUntil > _timing.CurTime)
            args.Modifiers.ModifySpeed(Setting("ConvertedStunSpeed"), Setting("ConvertedStunSpeed"), convertible: false);
    }

    public override void Update(float frameTime)
    {
        // The server owns periodic drain; movement permissions still use replicated stamina on both sides.
        if (_net.IsClient)
            return;
        _elapsed += frameTime;
        if (_elapsed < 0.25f)
            return;
        var elapsed = _elapsed;
        _elapsed = 0f;
        var query = EntityQueryEnumerator<SkillsComponent, SkillProgressionComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var skills, out var state, out var stamina))
        {
            if (SkillScaling.Level(skills, SharedSkillsSystem.EnduranceId) < SkillScaling.Legendary && state.ConvertedKnockdownUntil <= _timing.CurTime)
                continue;
            _movement.RefreshMovementSpeedModifiers(uid);
            if (Converts(uid))
            {
                var controlled = HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid) || state.ConvertedKnockdownUntil > _timing.CurTime;
                var moving = TryComp<InputMoverComponent>(uid, out var mover) && (mover.HeldMoveButtons & (MoveButtons.Up | MoveButtons.Down | MoveButtons.Left | MoveButtons.Right)) != 0;
                var cost = controlled ? Setting("StunStaminaPerSecond") : moving ? state.ConvertedSlowdown * Setting("SlowStaminaPerSecond") : 0f;
                if (cost > 0 && !Protected(uid))
                    _stamina.TakeStaminaDamage(uid, cost * elapsed, stamina, visual: false, ignoreResist: true);
            }
            if (!Converts(uid) && state.ConvertedKnockdownUntil > _timing.CurTime)
            {
                var remaining = state.ConvertedKnockdownUntil - _timing.CurTime;
                state.ConvertedKnockdownUntil = TimeSpan.Zero;
                Dirty(uid, state);
                _stun.TryKnockdown(uid, remaining, force: true);
            }
            _blocker.UpdateCanMove(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }
}
