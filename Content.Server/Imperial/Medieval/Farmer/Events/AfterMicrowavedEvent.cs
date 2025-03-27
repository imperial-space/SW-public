namespace Content.Server.Imperial.Medieval.Farmer;

[ByRefEvent]
public record struct AfterMicrowavedEvent(EntityUid Result, List<EntityUid> Users);
