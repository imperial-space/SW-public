namespace Content.Shared.DoAfter;

/// <summary>Adjust a timed interaction before its positions and network state are captured.</summary>
[ByRefEvent]
public record struct BeforeDoAfterStartEvent(DoAfterArgs Args, bool Cancelled = false);
