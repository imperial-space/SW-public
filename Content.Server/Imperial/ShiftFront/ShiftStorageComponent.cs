using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftStorageComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public int BioShlakBonus = 150;

    [DataField]
    public int PolymerBonus = 150;

    [DataField]
    public int NanoCarbonBonus = 50;

}
