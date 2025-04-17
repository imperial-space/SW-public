using Content.Server.Imperial.Medieval.Body;
using Content.Server.Imperial.Medieval.NeedSleep;
using Content.Server.Imperial.Medieval.RandomSteal;
using Content.Server.Imperial.Medieval.Weapons;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    [Dependency] private readonly MobThresholdSystem _threshold = default!;

    private void InitializeVitality()
    {
        SubscribeLocalEvent<SkillsComponent, GetSleepLevelModifiersEvent>(OnGetSleepModifiers);
        SubscribeLocalEvent<SkillsComponent, GetSuffocationDamageModifiersEvent>(OnModifySuffocationDamage);
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

    private void VitalityLevelSet(EntityUid uid, int level, int oldLevel)
    {
        var (proto, _) = GetSkill(uid, VitalityId);

        if (!TryComp<MobThresholdsComponent>(uid, out var mobState))
            return;

        var diff = level - oldLevel;
        _threshold.SetMobStateThreshold(uid,
                                        _threshold.GetThresholdForState(uid, MobState.Alive) + proto.Modifiers["AliveHealthPerLevel"] * diff,
                                        MobState.Alive);

        // Раскомментите при переносе в секретку
        // _threshold.SetMobStateThreshold(uid,
        //                                 _threshold.GetThresholdForState(uid, MobState.Wounded) + proto.Modifiers["WoundedHealthPerLevel"] * diff,
        //                                 MobState.Wounded);
    }
}
