using Content.Shared.Damage.Events;
using Content.Shared.Imperial.Medieval.Clothing;
using Content.Shared.Imperial.Medieval.Sprint;
using Content.Shared.Imperial.Medieval.Stamina;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string EnduranceId = "Endurance";

    private void InitializeEndurance()
    {
        SubscribeLocalEvent<SkillsComponent, GetSprintStaminaDamageModifiersEvent>(OnGetSprintStaminaDamageModifiers);
        SubscribeLocalEvent<SkillsComponent, GetStaminaRegenModifiersEvent>(OnModifyStaminaRegenModifiers);
        SubscribeLocalEvent<SkillsComponent, GetStaminaCritDurationModifiersEvent>(OnGetStaminaCritDurationModifiers);
        SubscribeLocalEvent<SkillsComponent, StaminaModifyEvent>(OnModifyStaminaDamage);
        SubscribeLocalEvent<SkillsComponent, CanSprintEvent>(OnCanSprint);
    }

    private void OnGetSprintStaminaDamageModifiers(EntityUid uid, SkillsComponent comp, ref GetSprintStaminaDamageModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveSprintStaminaDamageModifier"] : proto.Modifiers["NegativeSprintStaminaDamageModifier"]) * diff;
    }

    private void OnModifyStaminaRegenModifiers(EntityUid uid, SkillsComponent comp, ref GetStaminaRegenModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveStaminaRegenModifier"] : proto.Modifiers["NegativeStaminaRegenModifier"]) * diff;
    }

    private void OnGetStaminaCritDurationModifiers(EntityUid uid, SkillsComponent comp, ref GetStaminaCritDurationModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveStaminaCritModifier"] : proto.Modifiers["NegativeStaminaCritModifier"]) * diff;
    }

    private void OnModifyStaminaDamage(EntityUid uid, SkillsComponent comp, StaminaModifyEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);

        if (level < 20)
            return;

        args.Damage *= 1 + proto.Modifiers["MaxStaminaDamageModifier"];
    }

    private void OnCanSprint(EntityUid uid, SkillsComponent comp, ref CanSprintEvent args)
    {
        var (_, level) = GetSkill(uid, EnduranceId);

        if (level > 1)
            return;

        args.Cancelled = true;
    }

    private void EnduranceModifyClothingSpeedMod(EntityUid uid, SkillsComponent comp, ref ModifyClothingMovespeedModifierEvent args)
    {
        var (proto, level) = GetSkill(uid, EnduranceId);

        if (level <= 10)
            return;

        if (args.WalkMod > 1f || args.SprintMod > 1f)
            return;

        var diff = Math.Abs(level - 10);
        args.Walk += proto.Modifiers["PositiveSlowdownModifier"] * diff * (1 - args.Walk);
        args.Sprint += proto.Modifiers["PositiveSlowdownModifier"] * diff * (1 - args.Sprint);
    }
}
