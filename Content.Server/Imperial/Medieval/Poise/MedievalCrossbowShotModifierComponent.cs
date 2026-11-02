namespace Content.Server.Imperial.Medieval.Poise;

/// <summary>
/// Multiplies health and poise damage on bolts fired from a medieval crossbow.
/// Applied server-side to the spawned projectile; ammunition stays shared.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalCrossbowShotModifierComponent : Component
{
    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public float PoiseMultiplier = 1f;
}
