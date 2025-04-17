using Content.Shared.Imperial.Medieval.Skills;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    public override void Initialize()
    {
        base.Initialize();
        InitializeAgility();
        InitializeVitality();

        SubscribeLocalEvent<SkillsComponent, SkillLevelChangedEvent>(OnLevelChanged);
    }

    private void OnLevelChanged(EntityUid uid, SkillsComponent comp, ref SkillLevelChangedEvent args)
    {
        VitalityLevelSet(uid, args.Level, args.OldLevel);
    }
}
