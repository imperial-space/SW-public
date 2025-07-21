using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftREBComponent : Component
{
    [DataField]
    public string Faction = "";
}
