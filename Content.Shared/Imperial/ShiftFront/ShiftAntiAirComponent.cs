using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftAntiAirComponent : Component
{
    [DataField]
    public string Faction = "";

}
