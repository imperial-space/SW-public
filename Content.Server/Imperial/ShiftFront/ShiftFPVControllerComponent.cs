using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftFPVControllerComponent : Component
{
    [DataField]
    public EntityUid? LinkedDrone;

    [DataField]
    public bool NeedVR = true;

}
