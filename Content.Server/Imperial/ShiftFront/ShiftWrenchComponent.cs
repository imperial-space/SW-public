using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftWrenchComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier RegenDamage = new()
    {
        DamageDict = new()
            {
                { "Piercing", 25 },
                { "Blunt", 15 },
                { "Heat", 10 },
                { "Slash", 15 },
                { "Structural", 25 }
            }
    };
}
