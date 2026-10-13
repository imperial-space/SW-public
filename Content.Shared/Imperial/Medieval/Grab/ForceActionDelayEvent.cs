namespace Content.Shared.Imperial.Medieval.Grab;

/// <summary>Adjusts the delay before a grab is established.</summary>
[ByRefEvent]
public record struct ForceActionDelayEvent(float Seconds);
