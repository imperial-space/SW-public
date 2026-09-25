using Content.Server.Body.Components;
using System.Linq;
using Content.Server.Imperial.Medieval.Body;
using Content.Server.Imperial.Medieval.NeedSleep;
using Content.Shared.Imperial.Medieval.Body;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeVitality()
    {
        SubscribeLocalEvent<SkillsComponent, GetSleepLevelModifiersEvent>(OnGetSleepModifiers);
        SubscribeLocalEvent<SkillsComponent, GetSuffocationDamageModifiersEvent>(OnSuffocationDamage);
        SubscribeLocalEvent<SkillsComponent, GetBloodRegenModifiersEvent>(OnModifyBloodRegen);
    }

    private void OnGetSleepModifiers(EntityUid uid, SkillsComponent comp, ref GetSleepLevelModifiersEvent args)
    {
        if (args.Sleeping)
        {
            var (proto, level) = GetSkill(uid, VitalityId);
            args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["SleepRecoveryPerLevel"]);
        }
        else if (SkillScaling.Level(comp, EnduranceId) >= SkillScaling.Legendary)
            args.Modifier *= _proto.Index<SkillPrototype>(EnduranceId).Modifiers["NeedsMultiplier"];
    }

    private void OnModifyBloodRegen(EntityUid uid, SkillsComponent comp, ref GetBloodRegenModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["BloodRegenPerLevel"]);
    }

    private void OnSuffocationDamage(EntityUid uid, SkillsComponent comp, ref GetSuffocationDamageModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["SuffocationDamagePerLevel"]);
    }

    private void VitalityLevelSet(EntityUid uid, int level, int oldLevel)
    {
        var state = EnsureComp<SkillProgressionComponent>(uid);
        var (proto, _) = GetSkill(uid, VitalityId);
        var multiplier = SkillScaling.Multiplier(level, proto.Modifiers["HealthPerLevel"]);

        if (TryComp<SoftCritEmotesComponent>(uid, out var crit))
        {
            state.BaseSoftCritThreshold ??= crit.MinDamage;
            crit.MinDamage = state.BaseSoftCritThreshold.Value * multiplier;
        }

        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;
        if (state.BaseHealthThresholds.Count == 0)
        {
            foreach (var (damage, mobState) in thresholds.Thresholds)
            {
                if (mobState is not (MobState.Alive or MobState.Invalid))
                    state.BaseHealthThresholds[mobState] = damage;
            }
        }

        // Replace in one pass: intermediate overlapping thresholds must not change mob state.
        var updatedThresholds = new SortedDictionary<FixedPoint2, MobState>(thresholds.Thresholds);
        foreach (var entry in updatedThresholds.ToArray())
        {
            if (state.BaseHealthThresholds.ContainsKey(entry.Value))
                updatedThresholds.Remove(entry.Key);
        }
        foreach (var (mobState, damage) in state.BaseHealthThresholds)
            updatedThresholds[damage * multiplier] = mobState;
        _threshold.SetThresholds(uid, updatedThresholds, thresholds);
    }
}
