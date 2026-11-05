using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>Viewport geometry and elapsed-time accounting shared by the server and local presentation.</summary>
public static class MeleeLockOnMath
{
    public static bool ValidHalfExtents(Vector2 halfExtents)
        => float.IsFinite(halfExtents.X) && float.IsFinite(halfExtents.Y) &&
           halfExtents.X > 0 && halfExtents.Y > 0;

    /// <summary>One means the edge of a player-centered, rotated rectangular viewport.</summary>
    public static float ViewFraction(Vector2 delta, Vector2 halfExtents, Angle eyeRotation)
    {
        if (!ValidHalfExtents(halfExtents))
            return float.PositiveInfinity;

        var local = eyeRotation.RotateVec(delta);
        return MathF.Max(MathF.Abs(local.X) / halfExtents.X, MathF.Abs(local.Y) / halfExtents.Y);
    }

    /// <summary>
    /// Pan toward the target, optionally farther than the base threshold. The camera's
    /// extent is bounded without dropping the target or abruptly stopping the follow.
    /// </summary>
    public static Vector2 FollowOffset(Vector2 delta, Vector2 halfExtents, Angle eyeRotation, float threshold,
        float gain = 1f, float maxRange = float.PositiveInfinity)
    {
        var fraction = ViewFraction(delta, halfExtents, eyeRotation);
        if (!float.IsFinite(fraction) || fraction <= threshold)
            return Vector2.Zero;

        var followDistance = MathF.Max(0, MathF.Min(fraction, maxRange) - threshold);
        return delta * (followDistance / fraction) * gain;
    }

    /// <summary>
    /// Losing the target or exceeding the player's normal range starts the timeout.
    /// A target remaining visible in the extra camera-follow range must not reset it.
    /// </summary>
    public static bool LockAtRisk(bool targetVisible, float fraction, float maxRange)
        => !targetVisible || fraction > maxRange;

    public static bool Expired(TimeSpan now, TimeSpan? outsideSince, TimeSpan timeout)
        => outsideSince is { } since && now - since >= timeout;

    public static bool Warning(TimeSpan now, TimeSpan? expiresAt, TimeSpan warning)
        => expiresAt is { } deadline && deadline - now <= warning;

    public static TimeSpan ClampLockOnTime(TimeSpan remaining, TimeSpan capacity)
        => TimeSpan.FromTicks(Math.Clamp(remaining.Ticks, 0, Math.Max(0, capacity.Ticks)));

    /// <summary>Drain or gradually refill the Player's time for the previous tracking interval.</summary>
    public static TimeSpan AdvanceLockOnTime(TimeSpan remaining, TimeSpan? lastUpdated, TimeSpan now, bool draining,
        TimeSpan? recoveryStartedAt, TimeSpan capacity, TimeSpan recoveryDelay, float recoveryRate)
    {
        remaining = ClampLockOnTime(remaining, capacity);
        if (lastUpdated is not { } previous || now <= previous)
            return remaining;

        if (draining)
            return now - previous >= remaining ? TimeSpan.Zero : remaining - (now - previous);

        if (recoveryStartedAt is not { } recoveryStart || !float.IsFinite(recoveryRate) || recoveryRate <= 0)
            return remaining;

        var eligibleSince = recoveryStart + TimeSpan.FromTicks(Math.Max(0, recoveryDelay.Ticks));
        var from = previous > eligibleSince ? previous : eligibleSince;
        if (now <= from)
            return remaining;

        var available = TimeSpan.FromTicks(Math.Max(0, capacity.Ticks)) - remaining;
        var recoveredTicks = (now - from).Ticks * (double) recoveryRate;
        return recoveredTicks >= available.Ticks ? remaining + available : remaining + TimeSpan.FromTicks((long) recoveredTicks);
    }
}
