using Content.Shared.Imperial.Medieval.Clothing;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string VitalityId = "Vitality";

    private void InitializeVitality()
    {
        SubscribeLocalEvent<SkillsComponent, ModifyClothingMovespeedModifier>(OnModifyClothingSpeedMod);
    }

    private void OnModifyClothingSpeedMod(EntityUid uid, SkillsComponent comp, ref ModifyClothingMovespeedModifier args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level <= 10)
            return;

        if (args.WalkMod > 1f || args.SprintMod > 1f)
            return;

        var diff = Math.Abs(level - 10);
        args.Walk += proto.Modifiers["PositiveSlowdownModifier"] * diff * (1 - args.Walk);
    }


}
