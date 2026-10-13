namespace Content.Shared.Imperial.Dash;

[ByRefEvent]
public record struct DashAttemptEvent(bool Cancelled = false);

/// <summary>Allows a movement ability to perform an alternative dash after normal validation.</summary>
[ByRefEvent]
public record struct DashOverrideEvent(MedievalDashComponent Dash, Angle Rotation,
    float DistanceMultiplier, float CooldownMultiplier, bool Handled = false);
