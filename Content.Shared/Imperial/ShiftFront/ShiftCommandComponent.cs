using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftCommandComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public HashSet<ICommonSession> Players { get; set; } = new();

    [DataField]
    public List<ICommonSession> RespawnQueue { get; set; } = new();

}
