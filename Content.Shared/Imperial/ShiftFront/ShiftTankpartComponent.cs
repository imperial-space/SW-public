using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTankpartComponent : Component
{
    [DataField]
    public string Part = "";

    [DataField]
    public EntityUid? Tank;

}
