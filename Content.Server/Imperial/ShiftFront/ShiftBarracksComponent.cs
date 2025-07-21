using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftBarracksComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public int TimeTillNextGen = 60;
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnClone = "/Audio/Imperial/ShiftFront/clone_ready.ogg";

    [DataField]
    public int OverallGenTime = 60;

    [DataField]
    public int Boost = 0;

    [DataField]
    public string ChosenGen = "";
}
