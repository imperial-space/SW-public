namespace Content.Server.Imperial.Medieval.Poise;

/// <summary>
/// The base poise damage of a medieval bolt projectile. The active weapon may scale it.
/// Unlike PoiseImpact, this does not cause poise on manually thrown or spawned bolts.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalBoltPoiseComponent : Component
{
    [DataField]
    public int PoiseDamage;
}
