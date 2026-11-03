namespace Content.Shared.Imperial.Medieval.Magic.MedievalTargetOverlayOnAiming;


/// <summary>
/// The aimed spell can be cast only on an entity: a click on an empty place or on a target farther than
/// <see cref="Range" /> is ignored, so no mana is spent and no cooldown starts
/// </summary>
[RegisterComponent]
public sealed partial class MedievalRequireTargetEntityComponent : Component
{
    /// <summary>
    /// Maximum distance to the target in tiles, 0 means no limit
    /// </summary>
    [DataField]
    public float Range;
}
