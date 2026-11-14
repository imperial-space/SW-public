using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaSpectatorAnchorComponent : Component
{
    [DataField] public EntProtoId OrbPrototype = "BossArenaPonderingOrb";
    [DataField] public Vector2 Offset = new(2, 0);
    [DataField] public bool Enabled = true;
    public EntityUid Arena;
    public EntityUid? Orb;
}
