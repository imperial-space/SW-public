using Content.Shared.Damage;

namespace Content.Shared.Imperial.Medieval.Cult;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class DeathCurseComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier CurseDamage = new()
    {
        DamageDict = new()
        {
            { "Radiation", 0.5 }
        }
    };

    [DataField]
    public int CurseCount;
}
