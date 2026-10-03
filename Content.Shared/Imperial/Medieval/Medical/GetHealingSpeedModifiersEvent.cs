namespace Content.Shared.Imperial.Medieval.Medical;

[ByRefEvent]
public record struct GetHealingSpeedModifiersEvent(bool IsNotSelf, float Modifier = 1f);

[ByRefEvent]
public record struct GetMedicalHealingMultiplierEvent(float Multiplier = 1f);
