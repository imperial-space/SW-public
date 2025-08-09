using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTeamRespWaitComponent : Component
{
    [DataField]
    public string Faction = "";
}
