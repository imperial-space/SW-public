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
    public string DeathEffectProto = "";
    [DataField]
    public List<EntityUid> LinkedMipples = new();

}
