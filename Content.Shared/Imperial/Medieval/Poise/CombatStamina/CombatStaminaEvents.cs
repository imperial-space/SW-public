namespace Content.Shared.Damage.Events;

[ByRefEvent]
public record struct StaminaCostCheckEvent(float Cost, float CheckCost);

[ByRefEvent]
public record struct StaminaCostPaidEvent(float ActualCost);

/// <summary>Raised after resistance/global scaling, before stamina is changed.</summary>
[ByRefEvent]
public record struct StaminaFinalChangeEvent(float Value);

[ByRefEvent]
public record struct StaminaNaturalRegenAttemptEvent(bool Cancelled = false);

/// <summary>Allows a different recovery system to override ordinary extrapolation.</summary>
[ByRefEvent]
public record struct StaminaDamageQueryEvent(float Value);
