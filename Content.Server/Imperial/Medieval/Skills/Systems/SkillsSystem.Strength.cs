using Content.Server.Imperial.DeviceLinking;
using Content.Shared.Imperial.Medieval.Skills;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeStrength()
    {
        SubscribeLocalEvent<SkillsComponent, CheckSignalSwitchActivationTimeEvent>(OnCheckSignalSwitchActivationTime);
    }

    private void OnCheckSignalSwitchActivationTime(EntityUid uid, SkillsComponent comp, ref CheckSignalSwitchActivationTimeEvent args)
    {
        var (proto, level) = GetSkill(uid, StrengthId);
        args.Modifier /= SkillScaling.Multiplier(level, proto.Modifiers["ForceSpeedPerLevel"]);
    }
}
