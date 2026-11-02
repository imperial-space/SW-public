namespace Content.Shared.Imperial.TargetOverlay.Events;

/// <summary>Notifies melee lock-on that the existing ranged targeting session has ended.</summary>
public sealed class TargetOverlayStoppedEvent(EntityUid user) : EntityEventArgs
{
    public readonly EntityUid User = user;
}
