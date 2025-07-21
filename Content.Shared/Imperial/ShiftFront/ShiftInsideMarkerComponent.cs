using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftInsideMarkerComponent : Component
{
    [DataField]
    public string Inside = "";

}
