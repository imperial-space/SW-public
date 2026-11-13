using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>
/// Smooth Player-relative panning, bounded to keep the Player in view while allowing
/// the Target to leave it. Offsets are stored on the eye's axes.
/// </summary>
public sealed class MeleeLockOnCameraState
{
    public Vector2 Offset { get; private set; }

    public void Update(Vector2 desiredOffset, Vector2 viewHalfExtents, float playerEdgeLimit,
        bool followingAllowed, bool cameraEnabled, float frameTime)
    {
        if (!cameraEnabled || !float.IsFinite(viewHalfExtents.X) || !float.IsFinite(viewHalfExtents.Y) ||
            viewHalfExtents.X <= 0 || viewHalfExtents.Y <= 0)
        {
            Reset();
            return;
        }

        var limit = Math.Clamp(playerEdgeLimit, 0f, 0.999f);
        var desired = followingAllowed ? LimitOffset(desiredOffset, viewHalfExtents, limit) : Vector2.Zero;
        var blend = 1f - MathF.Exp(-8f * frameTime);
        // Clamp after smoothing too, so resizing or zooming cannot strand the Player
        // outside a smaller viewport while an old offset slowly catches up.
        Offset = LimitOffset(Vector2.Lerp(Offset, desired, blend), viewHalfExtents, limit);
    }

    private static Vector2 LimitOffset(Vector2 offset, Vector2 halfExtents, float limit)
    {
        var fraction = MathF.Max(MathF.Abs(offset.X) / halfExtents.X, MathF.Abs(offset.Y) / halfExtents.Y);
        if (!float.IsFinite(fraction))
            return Vector2.Zero;

        return fraction > limit ? offset * (limit / fraction) : offset;
    }

    public void Reset()
    {
        Offset = Vector2.Zero;
    }
}
