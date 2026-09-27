using Content.Server.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.Grab.Components;
using Content.Shared.Imperial.Medieval.Grab.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Skills.Progression;

public sealed partial class SkillPerkSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly GrabSystem _grabs = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    // Shared by the examination and strength-equipment sections.
    [Dependency] private readonly InventorySystem _inventory = default!;
    private static readonly EntProtoId RecoveryAction = "ActionSkillRecovery";

    private float Setting(string skill, string key) => _prototypes.Index<SkillPrototype>(skill).Modifiers[key];
    private bool HasLevel(EntityUid uid, string skill, int level) =>
        TryComp<SkillsComponent>(uid, out var skills) && SkillScaling.Level(skills, skill) >= level;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillProfileChangedEvent>(OnProfile);
        SubscribeLocalEvent<SkillsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SkillsComponent, SkillRecoveryActionEvent>(OnRecovery);
        InitializeExamination();
        InitializeStrength();
    }

    private void OnMapInit(EntityUid uid, SkillsComponent skills, ref MapInitEvent args) => RefreshActions(uid);
    private void OnProfile(ref SkillProfileChangedEvent args) => RefreshActions(args.Uid);

    private void RefreshActions(EntityUid uid)
    {
        var actions = EnsureComp<SkillGrantedActionsComponent>(uid);
        RefreshStrengthEquipment(uid, actions);
        if (HasLevel(uid, SharedSkillsSystem.EnduranceId, SkillScaling.Legendary))
        {
            _actions.AddAction(uid, ref actions.RecoveryAction, RecoveryAction);
            if (actions.RecoveryReadyAt > _timing.CurTime)
                _actions.SetCooldown(actions.RecoveryAction, actions.RecoveryReadyAt - _timing.CurTime);
        }
        else
        {
            _actions.RemoveAction(uid, actions.RecoveryAction);
            actions.RecoveryAction = null;
        }
    }

    private void OnRecovery(EntityUid uid, SkillsComponent skills, SkillRecoveryActionEvent args)
    {
        var actions = EnsureComp<SkillGrantedActionsComponent>(uid);
        if (args.Handled || _mobs.IsDead(uid) || !HasLevel(uid, SharedSkillsSystem.EnduranceId, SkillScaling.Legendary)
            || _timing.CurTime < actions.RecoveryReadyAt || !TryComp<StaminaComponent>(uid, out var stamina))
            return;
        var state = EnsureComp<SkillProgressionComponent>(uid);
        state.ConvertedKnockdownUntil = TimeSpan.Zero;
        state.ControlProtectionUntil = _timing.CurTime + TimeSpan.FromSeconds(Setting(SharedSkillsSystem.EnduranceId, "RecoveryProtection"));
        Dirty(uid, state);
        _stamina.RestoreStamina(uid, stamina);
        _stun.ClearStun(uid);
        _stun.ClearKnockdown(uid);
        if (TryComp<GrabbableComponent>(uid, out var grabbed) && grabbed.Grabber != null)
            _grabs.TryStopGrab(uid, grabbed, uid);
        actions.RecoveryReadyAt = _timing.CurTime + TimeSpan.FromSeconds(Setting(SharedSkillsSystem.EnduranceId, "RecoveryCooldown"));
        _actions.SetCooldown(actions.RecoveryAction, actions.RecoveryReadyAt - _timing.CurTime);
        _movement.RefreshMovementSpeedModifiers(uid);
        args.Handled = true;
    }
}
