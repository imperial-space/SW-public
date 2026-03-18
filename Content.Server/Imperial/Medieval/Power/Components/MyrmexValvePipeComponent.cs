using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Medieval.Power;

[RegisterComponent]
public sealed partial class MyrmexValvePipeComponent : Component
{
    [DataField]
    public string NodeId = "power";

    [DataField]
    public float DoAfterTime = 4f;
}
