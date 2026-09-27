namespace Content.Server.Imperial.Medieval.Skills.Progression;

[RegisterComponent]
public sealed partial class SkillMagicComponent : Component
{
    [DataField] public bool ProfessionKnown;
    [DataField] public bool ProfessionMage;
    [DataField] public bool MageRewardGranted;
    [DataField] public bool FreeSpellClaimed;
}

[RegisterComponent]
public sealed partial class SkillLearningStoreComponent : Component
{
}
