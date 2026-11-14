using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossSpreadSoulVisuals
{
    public const double FrameSeconds = 0.04;
    public const float DurationSeconds = 1.0f;
    public const double MarkerPeriodSeconds = 0.8;
    public const float PixelsPerTile = 32;
    public const float MarkerBottomExtent = 0.625f;
    public const float MarkerGap = 0.25f;
    public static readonly Vector2 Anchor = new(96, 96);

    public static bool ReplacesLegacyCue(BossTelegraphStylePrototype style, BossActorMarkerKind kind)
        => kind == BossActorMarkerKind.Spread && style.SpreadSoulTexture != null;

    public static int BurstFrame(double age)
        => !double.IsFinite(age) ? 0 : (int)Math.Clamp(Math.Floor(age / FrameSeconds + 1e-7), 0, 25);

    public static float Fade(double age)
        => !double.IsFinite(age) || age < 0 || age >= DurationSeconds ? 0 : (float)Math.Pow(1 - age, 0.8);

    public static float MarkerBob(double age)
        => (float)Math.Round(Math.Sin(age * 4)) / 16;

    public static Box2 Bounds(Vector2i size)
        => Box2.FromDimensions(new Vector2(-Anchor.X, Anchor.Y - size.Y) / PixelsPerTile,
            (Vector2)size / PixelsPerTile);

    public static UIBox2 TileRegion(int dx, int dy)
        => new(80 + dx * 32, 80 - dy * 32, 112 + dx * 32, 112 - dy * 32);
}
