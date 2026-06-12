using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Ships.Wave;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class WaveComponent : Component
{
    /// <summary>
    /// Damage specifier that is multiplied against the calculated damage amount to determine what damage is applied to the colliding entity.
    /// </summary>
    /// <remarks>
    /// The values of this should add up to 1 or else the damage will be scaled.
    /// </remarks>
    [DataField]
    public DamageSpecifier DamageTypes = new();

    /// <summary>
    /// A list of entities that this meteor has collided with. used to ensure no double collisions occur.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> HitList = new();

    [DataField]
    public float Strength = 1;
    [DataField]
    public float Direction = 1;

    [DataField]
    public bool DeleteOnCollide = true;
}
