namespace Content.Shared.Damage.Events;

/// <summary>Raised before an involuntary damage emote or interrupted speech, not voluntary speech.</summary>
[ByRefEvent]
public record struct PainReactionAttemptEvent(bool Cancelled = false);
