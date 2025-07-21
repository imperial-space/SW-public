using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTankTurretComponent : Component
{
    [DataField]
    public EntityUid? LinkedTank;

    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "AntiTank", 1 }
        }
    };
}
