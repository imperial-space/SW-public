using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftMedTowerComponent : Component
{
    [DataField]
    public string Faction = "";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier RegenDamage = new()
    {
        DamageDict = new()
            {
                { "Piercing", 2 },
                { "Asphyxiation", 5 },
                { "Bloodloss", 5 },
                { "Blunt", 5 },
                { "Heat", 5 },
                { "Poison", 3 },
                { "Slash", 1 },
                { "Shock", 5 },
                { "Radiation", 2 },
                { "Structural", 15 },
                { "AntiTank", 10 },
                { "Cellular", 1 }
            }
    };
}
