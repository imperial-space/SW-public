using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;


namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftConsoleResearchComponent : Component
{

    [DataField]
    public string Faction = "";

    [DataField]
    public int Points = 100;

    [DataField]
    public List<string> Researched = new();
}
