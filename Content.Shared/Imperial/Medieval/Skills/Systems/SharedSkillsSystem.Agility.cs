using Content.Shared.Imperial.Dash;
using Content.Shared.Imperial.Medieval.Climbing;
using Content.Shared.Imperial.Medieval.Inventory;
using Content.Shared.MedievalLockpick.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string AgilityId = "Agility";

    private void InitializeAgility()
    {
        SubscribeLocalEvent<SkillsComponent, GetMeleeAttackRateEvent>(OnGetRate);
        SubscribeLocalEvent<SkillsComponent, CheckDashCooldownModifiersEvent>(OnGetDashCooldown);
        SubscribeLocalEvent<SkillsComponent, CheckDashDistanceModifiersEvent>(OnGetDashDistance);
        SubscribeLocalEvent<SkillsComponent, CanDashEvent>(OnCanDash);
        SubscribeLocalEvent<SkillsComponent, GetClimbDelayModifiersEvent>(OnGetClimbDelayModifiers);
        SubscribeLocalEvent<SkillsComponent, GetLockpickChanceModifiersEvent>(OnGetLockpickModifiers);
        SubscribeLocalEvent<SkillsComponent, GetEquipDelayModifiersEvent>(OnGetEquipDelayModifiers);
    }

    private void OnGetRate(EntityUid uid, SkillsComponent comp, ref GetMeleeAttackRateEvent args)
    {
        if (!args.RaisedOnUser)
            return;
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Multipliers += SkillScaling.Multiplier(level, proto.Modifiers["AttackRatePerLevel"]) - 1f;
        args.Rate *= EntityManager.System<SkillActionSystem>().CriticalSpeed(uid);
    }

    private void OnCanDash(EntityUid uid, SkillsComponent comp, ref CanDashEvent args)
    {
        if (SkillScaling.Level(comp, AgilityId) < SkillScaling.Basic)
            args.Cancelled = true;
    }

    private void OnGetDashCooldown(EntityUid uid, SkillsComponent comp, ref CheckDashCooldownModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["DashCooldownPerLevel"]);
    }

    private void OnGetDashDistance(EntityUid uid, SkillsComponent comp, ref CheckDashDistanceModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["DashDistancePerLevel"]);
    }

    private void OnGetClimbDelayModifiers(EntityUid uid, SkillsComponent comp, ref GetClimbDelayModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Modifier /= SkillScaling.Multiplier(level, proto.Modifiers["PrecisionSpeedPerLevel"]);
    }

    private void OnGetLockpickModifiers(EntityUid uid, SkillsComponent comp, ref GetLockpickChanceModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["LockpickChancePerLevel"]);
    }

    private void OnGetEquipDelayModifiers(EntityUid uid, SkillsComponent comp, ref GetEquipDelayModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Modifier /= SkillScaling.Multiplier(level, proto.Modifiers["PrecisionSpeedPerLevel"]);
    }
}
