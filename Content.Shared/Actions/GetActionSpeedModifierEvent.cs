namespace Content.Shared.Actions;

/// <summary>Multiplies the speed of timed actions, attacks and movement abilities.</summary>
[ByRefEvent]
public record struct GetActionSpeedModifierEvent(float Multiplier = 1f);
