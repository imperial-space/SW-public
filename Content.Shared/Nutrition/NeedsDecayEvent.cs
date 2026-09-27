namespace Content.Shared.Nutrition;

/// <summary>Adjusts how quickly hunger and thirst deplete.</summary>
[ByRefEvent]
public record struct NeedsDecayEvent(float Multiplier = 1f);
