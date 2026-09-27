namespace Content.Shared.Medical.Healing;

/// <summary>Raised on the user before starting treatment with a medical item.</summary>
[ByRefEvent]
public record struct MedicalTreatmentAttemptEvent(EntityUid Item, EntityUid Target, bool Cancelled = false, string? Reason = null);
