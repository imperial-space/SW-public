using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftMapComponent : Component
{
    [DataField]
    public string Faction = "";
    [DataField]
    public float mapX = 131f;
    [DataField]
    public float mapY = 100f;
    [DataField]
    public float offsetX = 35f;
    [DataField]
    public float offsetY = -123f;
    [DataField]
    public float entX = 4f;
    [DataField]
    public float entY = 3f;

}
