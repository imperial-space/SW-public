using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;


namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftExtractorComponent : Component
{

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnConsume = "/Audio/Imperial/ShiftFront/consume.ogg";

    [DataField]
    public bool Digged = false;
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 99999 },
        }
    };

    [DataField]
    public string Faction = "";

    [DataField]
    public int TimeTillNextGen = 30;

    [DataField]
    public int OverallGenTime = 30;

    [DataField]
    public string Type = "";

    [DataField]
    public int Amount = 0;

    [DataField]
    public float AmountMult = 1f;
}
