using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Magic.MedievalShootAfterSpawn;


/// <summary>

/// </summary>
[RegisterComponent]
public sealed partial class MedievalShootAfterSpawnComponent : Component
{
    /// <summary>
    /// Delay before each shot
    /// </summary>
    [DataField]
    public TimeSpan ShootRate = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Next shot time
    /// </summary>
    [ViewVariables]
    public TimeSpan NextShot = TimeSpan.Zero;

    /// <summary>
    /// Shoot target
    /// </summary>
    [ViewVariables]
    public (MapCoordinates CursorPosition, EntityUid? TargetEntity) Target;

    /// <summary>
    /// A spell caster
    /// </summary>
    [ViewVariables]
    public EntityUid Performer;
}
