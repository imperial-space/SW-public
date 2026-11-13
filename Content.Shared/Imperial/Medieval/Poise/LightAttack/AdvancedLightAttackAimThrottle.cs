using System;
using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

public struct AdvancedLightAttackAimThrottle
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);
    private const float PositionToleranceSquared = 1f / (32f * 32f);
    private const float DirectionToleranceCos = 0.9998477f; // One degree.

    private uint _attackId;
    private TimeSpan _nextUpdate;

    public bool ShouldSend(uint attackId, TimeSpan now, TimeSpan strike, TimeSpan tickPeriod, bool significant)
    {
        var finalInput = strike - now <= tickPeriod + tickPeriod;
        if (!finalInput && (!significant || _attackId == attackId && now < _nextUpdate))
            return false;

        _attackId = attackId;
        _nextUpdate = now + Interval;
        return true;
    }

    public static bool IsSignificant(Vector2 previous, Vector2 current, float range)
    {
        var previousLength = previous.Length();
        var currentLength = current.Length();
        if (!float.IsFinite(previousLength) || !float.IsFinite(currentLength) || !float.IsFinite(range))
            return true;

        // Do not hide a direction change close to the attacker's center.
        if (previousLength > 0f && currentLength > 0f &&
            Vector2.Dot(previous / previousLength, current / currentLength) < DirectionToleranceCos)
            return true;

        range = MathF.Max(0f, range);
        if (previousLength > range && previousLength > 0f)
            previous *= range / previousLength;
        if (currentLength > range && currentLength > 0f)
            current *= range / currentLength;
        return Vector2.DistanceSquared(previous, current) >= PositionToleranceSquared ||
               (previousLength == 0f) != (currentLength == 0f);
    }
}
