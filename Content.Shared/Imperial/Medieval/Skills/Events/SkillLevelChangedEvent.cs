namespace Content.Shared.Imperial.Medieval.Skills;

[ByRefEvent]
public record struct SkillLevelChangedEvent(string Id, int Level, int OldLevel);
