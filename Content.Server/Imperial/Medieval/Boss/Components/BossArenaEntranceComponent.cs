using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaEntranceComponent : Component
{
    [DataField]
    public Vector2 ArrivalOffset = new(0, -1.5f);
}
