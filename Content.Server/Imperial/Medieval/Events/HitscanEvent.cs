namespace Content.Server.Weapons.Ranged.Systems;

[ByRefEvent]
public record struct HitscanResolvedEvent(EntityUid Target, EntityUid? Shooter);
