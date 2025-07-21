using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;


namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftResearchGenerateComponent : Component
{

    [DataField]
    public string Faction = "";

    [DataField]
    public int TimeTillNextGen = 30;

    [DataField]
    public int OverallGenTime = 30;

    [DataField]
    public int Points = 5;
}
