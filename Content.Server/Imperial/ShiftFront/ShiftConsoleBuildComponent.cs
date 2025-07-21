using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftConsoleBuildComponent : Component
{
    [DataField]
    public int CurrentBuildTimer = 0;

    [DataField]
    public int FutureTimer = 0;

    [DataField]
    public bool IsBuilding = false;

    [DataField]
    public string BuildingType = "";

    [DataField]
    public string BuildingCode = "";


    [DataField]
    public string Faction = "";

    [DataField]
    public EntityUid? BuildingLight;
    //[DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    //public string EffectSoundOnBuildStart = "/Audio/Imperial/ShiftFront/catapult_shot.ogg";
}
