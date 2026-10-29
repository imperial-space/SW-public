namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Broadcast after a profile refresh or map initialization so modules can initialize and update their state.</summary>
[ByRefEvent]
public readonly record struct SkillProfileChangedEvent(EntityUid Uid);
