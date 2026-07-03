using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeChanceSpawnComponent : Component
{
    [DataField("chance")]
    public float Chance = 0.5f;
    [DataField("entities")]
    public Dictionary<EntProtoId, float> Entities = new();
}
