using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeLinkedSpawnerComponent : Component
{
    public EntityUid? LinkedEntity;
    [DataField("time")]
    public TimeSpan RespawnTime = TimeSpan.FromSeconds(60);
    [DataField("mobs")]
    public Dictionary<EntProtoId, float> Mobs = new();
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? NextSpawn = TimeSpan.Zero;
}
