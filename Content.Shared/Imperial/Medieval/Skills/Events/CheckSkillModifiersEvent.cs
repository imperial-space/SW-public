namespace Content.Shared.Imperial.Medieval.Skills;

[ByRefEvent]
public record struct CheckSkillModifiersEvent(string Proto, string ActionId, float Modifier = 1f);
