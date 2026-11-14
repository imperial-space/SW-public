using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaRewardItemComponent : Component
{
    [DataField] public string Role = "coffer-weapon";
    [DataField] public EntProtoId Fallback = "MedievalScythe";
}
