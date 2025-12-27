namespace Content.Shared.Interaction;

[ByRefEvent]
public readonly record struct MedievalInteractionEvent(EntityUid User, EntityUid Target);
