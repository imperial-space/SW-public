namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>A directed notification after a skill level has been stored.</summary>
[ByRefEvent]
public readonly record struct SkillLevelChangedEvent(string Id, int Level, int OldLevel);
