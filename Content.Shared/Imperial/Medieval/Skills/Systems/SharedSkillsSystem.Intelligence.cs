using Content.Shared.Imperial.Medieval.Construction;
using Content.Shared.Paper;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string IntelligenceId = "Intelligence";

    private void InitializeIntelligence()
    {
        SubscribeLocalEvent<SkillsComponent, GetConstructionSpeedModifiersEvent>(OnGetConstructionSpeedModifiers);
        SubscribeLocalEvent<SkillsComponent, PaperWriteAttemptEvent>(OnCanWrite);
    }

    private void OnGetConstructionSpeedModifiers(EntityUid uid, SkillsComponent comp, ref GetConstructionSpeedModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveConstructionSpeedModifier"] : proto.Modifiers["NegativeConstructionSpeedModifier"]) * diff;
    }

    private void OnCanWrite(EntityUid uid, SkillsComponent comp, ref PaperWriteAttemptEvent args)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level > 5)
            return;

        args.Cancelled = true;
        args.FailReason = "Вы слишком глупы.";
    }
    public bool CanRead(EntityUid uid)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level > 5)
            return false;

        return true;
    }
}
