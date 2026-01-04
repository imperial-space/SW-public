using System.Numerics;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Trigger.Components.Effects;

/// <summary>
/// Spawns entities at a random offset from the center when triggered
/// </summary>
[RegisterComponent]
public sealed partial class RandomOffsetSpawnComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// The entity that will be copied to create
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SpawnedEntityID = string.Empty;
    /// <summary>
    /// Radius in which entities will be spawned
    /// </summary>
    [DataField(required: true)]
    public int Radius;
    /// <summary>
    /// Quantity of spawned entities
    /// </summary>
    [DataField(required: true)]
    public int Quantity;
}
