using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.SkillCheck;

[RegisterComponent]
public sealed partial class SkillCheckCooldownComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    [DataField]
    public EntProtoId DiePrototype = "MedievalSkillCheckDie";
}
