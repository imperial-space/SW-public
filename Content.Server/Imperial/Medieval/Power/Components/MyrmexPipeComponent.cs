using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Medieval.Power;

[RegisterComponent]
public sealed partial class MyrmexPipeComponent : Component
{
    [DataField]
    public MyrmexPipeType PipeType = MyrmexPipeType.Straight;
}

public enum MyrmexPipeType : byte
{
    Straight,
    Corner,
    TJunction,
    Cross
}
