using System.Numerics;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Trigger.Components.Effects;

/// <summary>
/// Spawn entities in any order specified in the List when triggered
/// How many vectors are set, the same number of objects will be spawned
/// </summary>
[RegisterComponent]
public sealed partial class VectoredSpawnOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// The entity that will be copied to create
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SpawnedEntityID = string.Empty;
    /// <summary>
    /// List of vectors that indicate the places where entities spawn, relative to the parent
    /// </summary>
    [DataField(required: true)]
    public List<Vector2> SpawnPositions;

}
