using System.Numerics;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

public sealed partial class SeaShipRippleOverlay
{
    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (MathF.Abs(edge1 - edge0) < float.Epsilon)
            return value >= edge1 ? 1f : 0f;

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Vector2 Normalize(Vector2 vector)
    {
        return vector.LengthSquared() <= 0.0001f ? Vector2.UnitY : Vector2.Normalize(vector);
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new Vector2(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos);
    }

    private static float GetWakeRadiusForWidth(float wakeWidth, float span)
    {
        var chordFactor = MathF.Max(2f * MathF.Sin(MathF.Max(span, 0.08f)), 0.18f);
        return MathF.Max(wakeWidth / chordFactor, wakeWidth * 0.28f);
    }

    private static float Hash(int value)
    {
        var hashed = MathF.Sin(value * 12.9898f) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private static float Frac(float value)
    {
        return value - MathF.Floor(value);
    }
}
