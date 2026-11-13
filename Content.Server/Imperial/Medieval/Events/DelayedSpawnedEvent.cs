namespace Content.Server.Spawners.EntitySystems;

[ByRefEvent]
public record struct DelayedSpawnedEvent(EntityUid SpawnedEntity);
