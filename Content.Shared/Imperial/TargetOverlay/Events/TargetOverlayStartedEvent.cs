namespace Content.Shared.Imperial.TargetOverlay.Events;

/// <summary>One hook when the existing projectile targeting session takes ownership.</summary>
public sealed class TargetOverlayStartedEvent(EntityUid user, EntityUid? sender) : EntityEventArgs
{
    public readonly EntityUid User = user;
    public readonly EntityUid? Sender = sender;
}
