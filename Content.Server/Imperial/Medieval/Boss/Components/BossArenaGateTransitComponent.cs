using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossArenaGateTransitComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan NextEntry;
}
