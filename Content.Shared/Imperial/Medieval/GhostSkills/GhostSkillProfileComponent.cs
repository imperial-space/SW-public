using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.GhostSkills;

[RegisterComponent]
public sealed partial class GhostSkillProfileComponent : Component
{
    public const int MinimumLevel = 5;
    public const int MaximumLevel = 15;

    [DataField]
    public EntProtoId ActionPrototype = "ActionConfigureGhostSkills";

    [DataField]
    public EntityUid? Action;

    [DataField]
    public Dictionary<string, int> Levels = new();
}
