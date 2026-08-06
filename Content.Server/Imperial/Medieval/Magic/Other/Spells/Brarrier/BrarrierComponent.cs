using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// Allows the entity to create a barrier in 4 directions from itself.
/// <para>

/// </para>
/// <para>
/// Yes, this is shit code.
/// </para>
/// </summary>
[RegisterComponent]
public sealed partial class BrarrierComponent : Component
{
    /// <summary>
    /// Technically, the radius of the fixture that will analyze all entities that fall into it, which will be checked with <see cref="LayersBlackList"/>
    /// </summary>
    [DataField(required: true)]
    public float LookupRadius;

    /// <summary>
    /// The entity that will be copied to create the barrier
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SpawnedEntity;

    /// <summary>
    /// Entity that will spawn if the starting entity is in an invalid position when checked with <see cref="LayersBlackList"/>
    /// <para>
    /// Useful if, for example, a barrier was spawned in a wall, but its copies should spread, for this we can spawn a prototype without textures and fixtures
    /// </para>
    /// </summary>
    [DataField]
    public EntProtoId? SpawnedOnInvalidLocation;

    /// <summary>
    /// Layers, with which we will not be able to cope with the barrier
    /// </summary>
    [DataField(customTypeSerializer: typeof(FlagSerializer<CollisionLayer>))]
    public int LayersBlackList = 0;

    /// <summary>
    /// Number of barrier iterations
    /// </summary>
    [DataField]
    public int BarrierLength = 1;

    /// <summary>
    /// If true, then the barrier will only spread in one direction.
    /// </summary>
    [DataField]
    public bool OnlyOneSide = false;

    /// <summary>
    /// The time it takes for the next barrier to spawn
    /// </summary>
    [DataField]
    public TimeSpan BarrierSpawnTime = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// The position that will be added to the initial one. Useful if we want to spawn, for example, a wall in front of the player
    /// </summary>
    [DataField]
    public Vector2 SpawnRelativePosition = new Vector2(1, 0);

    /// <summary>
    /// The percentage of intersection of fixtures above which we will not spawn a barrier.
    /// </summary>
    [DataField]
    public float PermissibleIntersectionsPercentage = 0.3f;

    /// <summary>
    /// A stack of barriers that should spawn.
    /// </summary>
    [ViewVariables]
    public List<(TimeSpan, MapCoordinates)> BarrierStack = new();
}
