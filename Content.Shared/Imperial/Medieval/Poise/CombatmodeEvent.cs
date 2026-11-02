namespace Content.Shared.CombatMode;

[ByRefEvent]
public record struct CombatModeCheck(bool OldValue, bool NewValue);
