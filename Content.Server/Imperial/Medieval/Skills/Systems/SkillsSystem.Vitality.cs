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
