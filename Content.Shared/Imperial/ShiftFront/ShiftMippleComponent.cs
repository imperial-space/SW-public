using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftMippleComponent : Component
{
    [DataField]
    public EntityUid? LinkedPlayer;
    [DataField]
    public EntityUid? LinkedMap;

}
