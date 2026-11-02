using System.Collections.Generic;

namespace Content.Shared.Imperial.Medieval.Poise;

/// <summary>
/// Server-owned poise data for a fired projectile, thrown spell, or contact attack.
/// </summary>
[RegisterComponent]
public sealed partial class PoiseImpactComponent : Component
{
    [DataField]
    public int PoiseDamage;

    // Set only on contact effects, such as a sunstrike zone or a boss spike.
    [DataField]
    public string? ContactFixture;

    [ViewVariables]
    public EntityUid? Shooter;

    [ViewVariables]
    public EntityUid? Weapon;

    public bool FromRangedWeapon;

    // Shared with delayed effects so the same attack cannot charge a target twice.
    public HashSet<EntityUid> HitTargets = new();
}
