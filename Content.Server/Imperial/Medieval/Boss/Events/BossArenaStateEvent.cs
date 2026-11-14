using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Boss;

public enum BossArenaState : byte
{
    Loaded,
    GateLocks,
    Victory,
    Reset,
    Closed,
    Reopened,
}

[ByRefEvent]
public struct BossArenaStateEvent(Entity<BossArenaMapComponent> arena, BossArenaState state)
{
    public Entity<BossArenaMapComponent> Arena = arena;
    public BossArenaState State = state;
    public bool CombatLocked;
    public bool EntryLocked;
}

public enum BossArenaPlayerChange : byte
{
    Joined,
    Left,
    Reviving,
    Transported,
    StopFalling,
}

[ByRefEvent]
public struct BossArenaPlayerEvent(EntityUid player, BossArenaPlayerChange change, EntityUid? arena = null, EntityUid? owner = null)
{
    public EntityUid Player = player;
    public BossArenaPlayerChange Change = change;
    public EntityUid? Arena = arena;
    public EntityUid? Owner = owner;
}

[ByRefEvent]
public struct BossArenaReturnEvent(EntityUid player, Entity<BossArenaMapComponent> arena, EntityCoordinates destination, bool valid)
{
    public EntityUid Player = player;
    public Entity<BossArenaMapComponent> Arena = arena;
    public EntityCoordinates Destination = destination;
    public bool Valid = valid;
    public bool AllowFallbackOwner;
}
