using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftResourceComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public int Amount = 10;

    [DataField]
    public string Type = "";
}
