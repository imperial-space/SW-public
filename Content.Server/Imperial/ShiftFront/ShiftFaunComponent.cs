using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftFaunComponent : Component
{
    [DataField]
    public int Heal = 100;

    [DataField]
    public int Amount = 10;
}
