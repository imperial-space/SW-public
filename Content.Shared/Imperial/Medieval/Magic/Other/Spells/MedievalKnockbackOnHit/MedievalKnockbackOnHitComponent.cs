namespace Content.Shared.Imperial.Medieval.Magic.MedievalKnockbackOnHit;


/// <summary>
/// Slightly pushes a living entity hit by this projectile in the flight direction
/// </summary>
[RegisterComponent]
public sealed partial class MedievalKnockbackOnHitComponent : Component
{
    [DataField]
    public float Distance = 0.5f;

    [DataField]
    public float Speed = 6f;
}
