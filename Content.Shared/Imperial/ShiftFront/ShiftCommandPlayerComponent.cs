using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftCommandPlayerComponent : Component
{
    [DataField]
    public string Faction = "";


    [DataField]
    public EntityUid? Command;


    [DataField]
    public int AFKTime = 0;

}
