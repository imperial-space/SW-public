using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossMovementRuneVisuals
{
    public const int CellPixels = 32;
    public const int ContentPixels = 24;
    public const float PixelsPerTile = 32;
    public const int CanvasPadding = (CellPixels - ContentPixels) / 2;
    public const float Size = ContentPixels / PixelsPerTile;
    public const float FrontOffset = 10 / PixelsPerTile;
    public const float DestinationScale = 2.30f;
    public const float DestinationPulseRange = .10f;
    public const float DestinationPulsePeriod = 2f;
    public const float FadeDistance = .45f;
    public const float DestinationClearance = ContentPixels / PixelsPerTile * DestinationScale * (1 + DestinationPulseRange) / 2 +
        ContentPixels / PixelsPerTile / 2 + .08f;
    public static UIBox2 ArrowRegion(Vector2i direction)
    {
        var column = direction.Y > 0 ? 0 : direction.X > 0 ? 1 : direction.Y < 0 ? 2 : 3;
        return new UIBox2(column * CellPixels + CanvasPadding, CanvasPadding,
            column * CellPixels + CanvasPadding + ContentPixels, CanvasPadding + ContentPixels);
    }

    public static UIBox2 DestinationRegion => new(CanvasPadding, CanvasPadding,
        CanvasPadding + ContentPixels, CanvasPadding + ContentPixels);

    public static Box2 CenteredBounds(Vector2 center) => Box2.FromDimensions(center - new Vector2(Size / 2), new Vector2(Size));
    public static Box2 DestinationBounds(Vector2 center, TimeSpan now)
    {
        var phase = (float)((now.TotalSeconds % DestinationPulsePeriod) / DestinationPulsePeriod * Math.Tau);
        var pulse = 1 + DestinationPulseRange * .5f * (1 - MathF.Cos(phase));
        var size = Size * DestinationScale * pulse;
        return Box2.FromDimensions(center - new Vector2(size / 2), new Vector2(size));
    }

    private static float SmoothFade(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    public static IEnumerable<(Box2 Bounds, UIBox2 Region, float Opacity)> MovementPatches(IReadOnlyList<Vector2i> path, TimeSpan now)
    {
        if (path.Count < 2)
            yield break;
        var delta = path[^1] - path[0];
        if (delta == Vector2i.Zero || delta.X != 0 && delta.Y != 0)
            yield break;
        var axis = new Vector2i(Math.Sign(delta.X), Math.Sign(delta.Y));
        var direction = new Vector2(axis.X, axis.Y);
        var origin = new Vector2(path[0].X + .5f, path[0].Y + .5f);
        var destination = new Vector2(path[^1].X + .5f, path[^1].Y + .5f);
        var length = Vector2.Distance(origin, destination);
        var phase = (float)((now.TotalSeconds * 1.5) % 1);
        var source = ArrowRegion(axis);
        for (var distance = length + phase; distance > 0; distance -= 1)
        {
            var progress = distance - FrontOffset;
            var opacity = SmoothFade(progress / FadeDistance) * SmoothFade((length - DestinationClearance - progress) / FadeDistance);
            if (opacity <= 0)
                continue;
            var center = origin + direction * progress;
            var full = CenteredBounds(center);
            var left = full.Left;
            var right = full.Right;
            var bottom = full.Bottom;
            var top = full.Top;
            if (axis.X != 0)
            {
                left = Math.Max(left, Math.Min(origin.X, destination.X));
                right = Math.Min(right, Math.Max(origin.X, destination.X));
            }
            else
            {
                bottom = Math.Max(bottom, Math.Min(origin.Y, destination.Y));
                top = Math.Min(top, Math.Max(origin.Y, destination.Y));
            }

            if (right <= left || top <= bottom)
                continue;
            yield return (new Box2(left, bottom, right, top), new UIBox2(
                source.Left + (left - full.Left) * PixelsPerTile,
                source.Top + (full.Top - top) * PixelsPerTile,
                source.Left + (right - full.Left) * PixelsPerTile,
                source.Top + (full.Top - bottom) * PixelsPerTile), opacity);
        }
    }
}
