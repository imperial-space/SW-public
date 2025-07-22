using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Content.Shared.Mind;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTankTurretComponent : Component
{
    [DataField]
    public MindComponent? Mind;
    [DataField]
    public EntityUid? LinkedTank;
    [DataField]
    public EntityUid? User;

    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "AntiTank", 1 }
        }
    };
}
[RegisterComponent]
public sealed partial class ShiftTankPilotComponent : Component
{
    [DataField]
    public EntityUid? Tank;
}
