using Content.Shared.Damage;
namespace Content.Server.Imperial.Medieval.Cult.Bloodspells.light;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class DeathCusreComponent : Component
{
    // [DataField("deathCurseTick")]
    // public float DeathCurseTick = 10f; туду сделать редактируемое
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier CurseDamage = new()
    {
        DamageDict = new()
        {
            { "Radiation", 0.5 }
        }
    };
}
