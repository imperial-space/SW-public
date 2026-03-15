using System.Numerics;

namespace Content.Shared.Movement.Events;

/// <summary>
/// Raised on an entity to check if it can move while weightless.
/// </summary>
[ByRefEvent]
public record struct CanWeightlessMoveEvent(EntityUid Uid)
{
    public bool CanMove = false;
}

[ByRefEvent]
public record struct WishDirOverrideEvent(EntityUid Uid, Vector2 WishDir)
{
    public Vector2 WishDir = WishDir;
}
