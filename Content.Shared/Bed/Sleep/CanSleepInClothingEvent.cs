namespace Content.Shared.Bed.Sleep;

/// <summary>Allows a sleeper to ignore the usual restrictions on clothing worn in bed.</summary>
[ByRefEvent]
public record struct CanSleepInClothingEvent(bool Allowed = false);
