namespace Content.Shared.Damage;

/// <summary>Raised on a moving entity before its collision deals impact damage.</summary>
[ByRefEvent]
public record struct ImpactDamageAttemptEvent(bool Cancelled = false);
