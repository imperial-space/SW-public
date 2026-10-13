namespace Content.Shared.Mobs;

/// <summary>Allows actions in critical condition without changing the actual health state.</summary>
[ByRefEvent]
public record struct CanActInCriticalEvent(bool Allowed = false);
