namespace Content.Server.Imperial.Medieval.Boss;

[ByRefEvent]
public readonly record struct BossArenaFallEvent(EntityUid Player, EntityUid Arena);
