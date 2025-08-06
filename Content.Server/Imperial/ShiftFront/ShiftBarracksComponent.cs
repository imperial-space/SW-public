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
    public int TimeTillNextGen = 30;

    [DataField]
    public int PassiveCloneTimer = 30;
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnClone = "/Audio/Imperial/ShiftFront/clone_ready.ogg";

    [DataField]
    public int Boost = 0;

    [DataField]
    public string ChosenGen = "";

    [DataField]
    public Dictionary<string, int> AvailableClasses = new Dictionary<string, int>
    {
        { "Fast", 0 },
        { "Assault", 0 },
        { "Eng", 0 },
        { "Mg", 0 },
        { "Sniper", 0 },
        { "Marksman", 0 },
        { "Bomber", 0 },
        { "Flanker", 0 },
        { "Med", 0 }
    };

}
