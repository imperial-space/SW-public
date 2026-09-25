using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Action permissions and speed, without changing a character's actual mob state.</summary>
public sealed class SkillActionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SkillWorkbenchSystem _workbenches = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillsComponent, BeforeDoAfterStartEvent>(OnDoAfter);
        SubscribeLocalEvent<SkillsComponent, CanActInCriticalEvent>(OnCriticalPermission);
        SubscribeLocalEvent<SkillsComponent, GetHealingSpeedModifiersEvent>(OnHealingSpeed);
        SubscribeLocalEvent<SkillsComponent, GetMedicalHealingMultiplierEvent>(OnHealingPower);
        SubscribeLocalEvent<SkillsComponent, RefreshMovementSpeedModifiersEvent>(OnMovement);
        SubscribeLocalEvent<SkillsComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<SkillProfileChangedEvent>(OnProfile, after: new[] { typeof(SkillResourceSystem) });
    }

    private float Modifier(SkillsComponent skills, string id, string key) =>
        SkillScaling.Multiplier(SkillScaling.Level(skills, id), _prototypes.Index<SkillPrototype>(id).Modifiers[key]);

    public float CriticalSpeed(EntityUid uid) =>
        _mobs.IsCritical(uid) && TryComp<SkillsComponent>(uid, out var skills)
            && SkillScaling.Level(skills, SharedSkillsSystem.VitalityId) >= SkillScaling.Legendary
            ? _prototypes.Index<SkillPrototype>(SharedSkillsSystem.VitalityId).Modifiers["CriticalSpeed"]
            : 1f;

    private void OnCriticalPermission(EntityUid uid, SkillsComponent skills, ref CanActInCriticalEvent args)
    {
        args.Allowed |= SkillScaling.Level(skills, SharedSkillsSystem.VitalityId) >= SkillScaling.Legendary;
    }

    private void OnDoAfter(EntityUid uid, SkillsComponent skills, ref BeforeDoAfterStartEvent args)
    {
        if (args.Args.Target is { } target && !_workbenches.CanUse(uid, target))
            args.Cancelled = true;
        if (SkillScaling.Level(skills, SharedSkillsSystem.VitalityId) >= SkillScaling.Expert)
            args.Args.BreakOnDamage = false;
        if (args.Args.AllowMovementAssistance && SkillScaling.Level(skills, SharedSkillsSystem.EnduranceId) >= SkillScaling.Expert)
        {
            args.Args.BreakOnMove = false;
            // Keep the target/tool within reach even though the starting position no longer matters.
            args.Args.DistanceThreshold ??= 2f;
        }
        args.Args.Delay /= CriticalSpeed(uid);
    }

    private void OnHealingSpeed(EntityUid uid, SkillsComponent skills, ref GetHealingSpeedModifiersEvent args) =>
        args.Modifier /= Modifier(skills, SharedSkillsSystem.IntelligenceId, "HealingSpeedPerLevel");

    private void OnHealingPower(EntityUid uid, SkillsComponent skills, ref GetMedicalHealingMultiplierEvent args) =>
        args.Multiplier *= Modifier(skills, SharedSkillsSystem.IntelligenceId, "MedicalPowerPerLevel");

    private void OnMovement(EntityUid uid, SkillsComponent skills, RefreshMovementSpeedModifiersEvent args) =>
        args.ModifySpeed(CriticalSpeed(uid), CriticalSpeed(uid), convertible: false);

    private void OnMobState(EntityUid uid, SkillsComponent skills, ref MobStateChangedEvent args) =>
        _movement.RefreshMovementSpeedModifiers(uid);

    private void OnProfile(ref SkillProfileChangedEvent args)
    {
        var uid = args.Uid;
        _blocker.UpdateCanMove(uid);
        _movement.RefreshMovementSpeedModifiers(uid);
        if (!_mobs.IsCritical(uid))
            return;
        if (_mobs.CanActInCritical(uid))
            _standing.Stand(uid);
        else
            _standing.Down(uid);
    }
}
