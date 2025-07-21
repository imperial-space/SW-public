using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftResPointComponent : Component
{
    [DataField("resType")]
    public string Type = "";

    [DataField]
    public int Amount = 15;

}
