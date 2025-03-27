namespace Content.Server.Imperial.Medieval.Farmer;

[ByRefEvent]
public record struct UserAfterHarvestEvent(EntityUid Harvested);
