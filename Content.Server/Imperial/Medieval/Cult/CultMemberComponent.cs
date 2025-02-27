using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultMemberComponent : Component
{
    [DataField]
    public EntityUid? parent;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
            {
                { "Poison", 10 },
                { "Asphyxiation", 10}
            }
    };
}
