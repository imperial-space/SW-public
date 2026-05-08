using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

[RegisterComponent]
public sealed partial class PlayerDrowningComponent : Component
{
    [DataField("drownTime")]
    public int DrownTime;

    [DataField("maxDrownTime")]
    public int MaxDrownTime = 25;

    [DataField("speedModifier")]
    public float SpeedModifier = 0.5f;

    [DataField("drowningDamage")]
    public DamageSpecifier DrowningDamage = new()
    {
        DamageDict = new()
        {
            { "Asphyxiation", 10 }
        }
    };
}
