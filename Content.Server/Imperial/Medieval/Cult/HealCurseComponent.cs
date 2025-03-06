using Content.Shared.Damage;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class HealCurseComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier RegenDamage = new()
    {
        DamageDict = new()
            {
                { "Radiation", 30.4 }
            }
    };
}
