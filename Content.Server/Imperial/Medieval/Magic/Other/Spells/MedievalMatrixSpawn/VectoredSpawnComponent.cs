using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// Spawn entities in any order specified in the List
/// How many vectors are set, the same number of objects will be spawned
/// </summary>
[RegisterComponent]
public sealed partial class VectoredSpawnComponent : Component
{
    /// <summary>
    /// The entity that will be copied to create
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SpawnedEntityID = string.Empty;
    /// <summary>
    /// List of offset vectors
    /// </summary>
    [DataField(required: true)]
    public List<Vector2> SpawnPositionsOffset;

}
