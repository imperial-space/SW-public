using Content.Shared.Damage.Events;
using Content.Shared.Imperial.Medieval.Sprint;
using Content.Shared.Imperial.Medieval.Stamina;
using Content.Shared.Weapons.Melee.Components;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string EnduranceId = "Endurance";

    private void InitializeEndurance()
    {
        SubscribeLocalEvent<SkillsComponent, GetSprintStaminaDamageModifiersEvent>(OnGetSprintStaminaDamageModifiers);
        SubscribeLocalEvent<SkillsComponent, GetStaminaRegenModifiersEvent>(OnModifyStaminaRegenModifiers);
        SubscribeLocalEvent<SkillsComponent, GetStaminaCritDurationModifiersEvent>(OnGetStaminaCritDuration);
        SubscribeLocalEvent<SkillsComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);
        SubscribeLocalEvent<SkillsComponent, MeleeThrowOnHitStartEvent>(OnModifyMeleeThrowDistance);
    }

    private void OnGetSprintStaminaDamageModifiers(EntityUid uid, SkillsComponent comp, ref GetSprintStaminaDamageModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["SprintCostPerLevel"]);
    }

    private void OnModifyStaminaRegenModifiers(EntityUid uid, SkillsComponent comp, ref GetStaminaRegenModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["StaminaRegenPerLevel"]);
    }

    private void OnModifyMeleeThrowDistance(EntityUid uid, SkillsComponent comp, ref MeleeThrowOnHitStartEvent args)
    {
        if (SkillScaling.Level(comp, EnduranceId) >= SkillScaling.Master)
            args.Distance = 0f;
    }

    private void OnGetStaminaCritDuration(EntityUid uid, SkillsComponent comp, ref GetStaminaCritDurationModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["StaminaCritDurationPerLevel"]);
    }

    private void OnStaminaDamage(EntityUid uid, SkillsComponent comp, ref BeforeStaminaDamageEvent args)
    {
        if (args.Value > 0 && SkillScaling.Level(comp, EnduranceId) >= SkillScaling.Legendary)
            args.Value *= _proto.Index<SkillPrototype>(EnduranceId).Modifiers["LegendaryStaminaDamageMultiplier"];
    }

}
