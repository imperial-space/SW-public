using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftSuppliesComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnSupply = "/Audio/Imperial/ShiftFront/supplies_ready.ogg";

    [DataField]
    public string Faction = "";

    [DataField]
    public int TimeTillNextGen = 60;

    [DataField]
    public int OverallGenTime = 60;

    [DataField]
    public string ChosenGen = "";

    [DataField]
    public bool Drone = false;
}
