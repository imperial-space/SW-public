using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftShowOnMapComponent : Component
{
    [DataField]
    public string Faction = "";
    [DataField]
    public string MippleProto = "";
    [DataField]
    public EntityUid? LinkedMipple;

}
