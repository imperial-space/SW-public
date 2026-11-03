using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalLaunchAfterSpawn;


/// <summary>
/// After an aimed spawn waits near the caster, then launches <see cref="Projectile" /> at the selected target
/// </summary>
[RegisterComponent]
public sealed partial class MedievalLaunchAfterSpawnComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    /// <summary>
    /// Time before the launch in seconds
    /// </summary>
    [DataField]
    public float Delay = 0.8f;

    /// <summary>
    /// Extra delay for each next entity spawned by the same cast
    /// </summary>
    [DataField]
    public float DelayStep = 0.2f;

    /// <summary>
    /// How far from the caster the entity appears
    /// </summary>
    [DataField]
    public float Radius = 0.85f;

    /// <summary>
    /// Angle in degrees between neighbouring entities of one cast: 0, +angle, -angle, +2*angle...
    /// </summary>
    [DataField]
    public float SlotAngle = 40f;

    [DataField]
    public float Speed = 12f;

    [ViewVariables]
    public TimeSpan SpawnedAt;

    [ViewVariables]
    public TimeSpan? LaunchAt;

    [ViewVariables]
    public EntityUid Performer;

    [ViewVariables]
    public (MapCoordinates CursorPosition, EntityUid? TargetEntity) Target;
}
