using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.RandomPoolSpawner;

[RegisterComponent]
public sealed partial class RandomPoolSpawnerComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Pool = new();

    [DataField(required: true)]
    public string GroupId;

    [DataField]
    public float ExtraChance = 0.5f;

    [DataField]
    public int ExtraAttempts = 4;

    [DataField]
    public bool DeleteAfterSpawn = true;
}
