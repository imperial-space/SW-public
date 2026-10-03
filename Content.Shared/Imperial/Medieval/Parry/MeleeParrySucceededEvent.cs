namespace Content.Shared.MeleeParry;

/// <summary>Raised on the defender after successfully parrying a melee attack.</summary>
[ByRefEvent]
public record struct MeleeParrySucceededEvent(EntityUid Attacker);
