using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class ChargingRuneExplosionComponent : Component
{
    [DataField]
    public float Time = 25f;

    public TimeSpan ExplodeTime = TimeSpan.Zero;

    public TimeSpan NextCheck = TimeSpan.Zero;
}
