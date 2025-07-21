using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftStructureComponent : Component
{
    [DataField]
    public string Structure = "";

    [DataField]
    public string Faction = "";
}
