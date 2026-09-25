namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Broadcast after attributes change so independent skill modules can refresh the same entity.</summary>
[ByRefEvent]
public readonly record struct SkillProfileChangedEvent(EntityUid Uid);
