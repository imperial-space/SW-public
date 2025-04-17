using Content.Server.Imperial.Medieval.RandomSteal;
using Content.Server.Imperial.Medieval.Weapons;
using Content.Shared.Imperial.Medieval.Skills;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeAgility()
    {
        SubscribeLocalEvent<SkillsComponent, GetGunSpreadModifiersEvent>(OnGetSpreadMod);
        SubscribeLocalEvent<SkillsComponent, GetStealChanceModifiersEvent>(OnGetStealChanceMod);
        SubscribeLocalEvent<SkillsComponent, TryGetAdditionalStealTargetEvent>(OnTryGetAdditionalStealTarget);

    }

    private void OnGetSpreadMod(EntityUid uid, SkillsComponent component, ref GetGunSpreadModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveSpreadModifier"] : proto.Modifiers["NegativeSpreadModifier"]) * diff;
    }

    private void OnGetStealChanceMod(EntityUid uid, SkillsComponent component, ref GetStealChanceModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level == 10)
            return;

        if (level <= 1)
        {
            args.Modifier = 0f;
            return;
        }

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveStealChanceModifier"] : proto.Modifiers["NegativeStealChanceModifier"]) * diff;
    }

    private void OnTryGetAdditionalStealTarget(EntityUid uid, SkillsComponent component, ref TryGetAdditionalStealTargetEvent args)
    {
        var (_, level) = GetSkill(uid, AgilityId);

        if (level < 20)
            return;

        args.Success = true;
    }
}
