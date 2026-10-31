using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaSpectatorOrbComponent : Component
{
    public EntityUid Anchor;
    public EntityUid Arena;
    public EntityUid Camera;
    public Dictionary<EntityUid, ICommonSession> Viewers = new();
}
