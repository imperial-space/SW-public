using Content.Server.Imperial.Medieval.Body;
using Content.Server.Imperial.Medieval.NeedSleep;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeVitality()
    {
        SubscribeLocalEvent<SkillsComponent, GetSleepLevelModifiersEvent>(OnGetSleepModifiers);
        SubscribeLocalEvent<SkillsComponent, GetSuffocationDamageModifiersEvent>(OnModifySuffocationDamage);
        SubscribeLocalEvent<SkillsComponent, GetBloodRegenModifiersEvent>(OnModifyBloodRegen);

    }

    private void OnGetSleepModifiers(EntityUid uid, SkillsComponent comp, ref GetSleepLevelModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);
        args.Modifier += (level > 10 ? proto.Modifiers["PositiveSleepEffeciencyModifier"] : proto.Modifiers["NegativeSleepEffeciencyModifier"]) * diff;
    }

    private void OnModifySuffocationDamage(EntityUid uid, SkillsComponent comp, ref GetSuffocationDamageModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);
        args.Modifier += (level > 10 ? proto.Modifiers["PositiveSuffocationDamageModifier"] : proto.Modifiers["NegativeSuffocationDamageModifier"]) * diff;
    }

    private void OnModifyBloodRegen(EntityUid uid, SkillsComponent comp, ref GetBloodRegenModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);
        args.Modifier += (level > 10 ? proto.Modifiers["PositiveBloodRegenModifier"] : proto.Modifiers["NegativeBloodRegenModifier"]) * diff;
    }

    private void VitalityLevelSet(EntityUid uid, int level, int oldLevel)
    {
        var (proto, _) = GetSkill(uid, VitalityId);

        var diff = level - oldLevel;
        _threshold.SetMobStateThreshold(uid,
                                        _threshold.GetThresholdForState(uid, MobState.Alive) + proto.Modifiers["AliveHealthPerLevel"] * diff,
                                        MobState.Alive);

        // _threshold.SetMobStateThreshold(uid,
        //                                 _threshold.GetThresholdForState(uid, MobState.Wounded) + proto.Modifiers["AliveHealthPerLevel"] * diff,
        //                                 MobState.Wounded);

        _threshold.SetMobStateThreshold(uid,
                                        _threshold.GetThresholdForState(uid, MobState.Critical) + proto.Modifiers["AliveHealthPerLevel"] * diff,
                                        MobState.Critical);

        _threshold.SetMobStateThreshold(uid,
                                        _threshold.GetThresholdForState(uid, MobState.Dead) + proto.Modifiers["AliveHealthPerLevel"] * diff,
                                        MobState.Dead);

        // var woundedThreshold = _threshold.GetThresholdForState(uid, MobState.Alive) + proto.Modifiers["AliveHealthPerLevel"] * diff;
        // if (level >= 20)
        //     woundedThreshold += proto.Modifiers["MaxHealthBonus"];
        // else if (oldLevel >= 20 && level < 20)
        //     woundedThreshold -= proto.Modifiers["MaxHealthBonus"];

        // _threshold.SetMobStateThreshold(uid,
        //                                 woundedThreshold,
        //                                 MobState.Wounded);

        // _threshold.SetMobStateThreshold(uid,
        //                                 woundedThreshold,
        //                                 MobState.Critical);

        // _threshold.SetMobStateThreshold(uid,
        //                                 woundedThreshold,
        //                                 MobState.Dead);
    }
}
