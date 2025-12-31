using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Collections.Generic;

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

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public Queue<(string message, TimeSpan time)> LastSpokenMessages = new();

    [DataField]
    public bool DeathCusre = false;
}
