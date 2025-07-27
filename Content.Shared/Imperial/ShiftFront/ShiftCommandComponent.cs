using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftCommandComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public HashSet<EntityUid> Players { get; set; } = new();

    [DataField]
    public HashSet<EntityUid> RespawnQueue { get; set; } = new();

}
