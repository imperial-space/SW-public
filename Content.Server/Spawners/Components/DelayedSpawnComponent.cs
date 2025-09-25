using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Spawners.Components;
/// <summary>
/// Use this to spawn a single entity
/// </summary>
[RegisterComponent]
public sealed partial class DelayedSpawnComponent : Component
{
    /// <summary>
    /// Entity that will be spawned
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Proto = string.Empty;

    /// <summary>
    /// Spawn delay
    /// </summary>
    [DataField]
    public int Delay = 5;

    /// <summary>
    /// Is needed to attach the spawned entity to the parent
    /// </summary>
    [DataField]
    public bool Attached = false;

    [DataField]
    public TimeSpan ParentSpawnTime = TimeSpan.FromSeconds(0);
    [DataField]
    public bool IsSpawned = false;
}
