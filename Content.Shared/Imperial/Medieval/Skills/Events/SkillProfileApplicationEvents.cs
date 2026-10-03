namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Directed notification before applying a character's profile; allows modules to record profession context.</summary>
[ByRefEvent]
public readonly record struct SkillProfileApplyingEvent(string? JobId);

/// <summary>Directed notification after the profile and its effects have been applied.</summary>
[ByRefEvent]
public readonly record struct SkillProfileAppliedEvent(string? JobId);
