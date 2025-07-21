using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftConsoleResourceComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public int BioShlak = 100;

    [DataField]
    public int Polymer = 300;

    [DataField]
    public int NanoCarbon = 0;

    [DataField]
    public int BioShlakLimit = 300;

    [DataField]
    public int PolymerLimit = 300;

    [DataField]
    public int NanoCarbonLimit = 100;


}
