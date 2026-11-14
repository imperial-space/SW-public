using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossRiverVisuals
{
    public const float DurationSeconds = 2.2f;
    public const double FrameSeconds = 0.02;
    public const int FrameCount = 110;
    public const float PixelsPerTile = 16;
    public const string State = "animation";
    public static readonly Vector2 AnchorPixels = new(128, 128);

    public static ResPath? Texture(BossTelegraphStylePrototype style, BossTelegraphPattern pattern,
    Vector2i origin, int halfSize, int facing = 0)
    {
        if (halfSize != 5) return null;
        if (pattern == BossTelegraphPattern.CardinalCone)
        {
            if (origin == new Vector2i(0, 7) && facing == 0) return style.ConeRiverNorthTexture;
            if (origin == new Vector2i(7, 0) && facing == 3) return style.ConeRiverEastTexture;
            if (origin == new Vector2i(0, -7) && facing == 2) return style.ConeRiverSouthTexture;
            if (origin == new Vector2i(-7, 0) && facing == 1) return style.ConeRiverWestTexture;
            return null;
        }
        if (pattern == BossTelegraphPattern.Divide)
            return origin == Vector2i.Zero ? style.DivideRiverTexture : null;
        if (pattern != BossTelegraphPattern.Cross) return null;
        if (origin == Vector2i.Zero) return style.CrossRiverTexture;
        if (origin == new Vector2i(0, 4)) return style.CrossRiverNorthTexture;
        if (origin == new Vector2i(4, 0)) return style.CrossRiverEastTexture;
        if (origin == new Vector2i(0, -4)) return style.CrossRiverSouthTexture;
        if (origin == new Vector2i(-4, 0)) return style.CrossRiverWestTexture;
        return null;
    }

    public static int FrameIndex(double elapsed)
        => !double.IsFinite(elapsed) ? 0 : (int)Math.Clamp(Math.Floor(elapsed / FrameSeconds + 1e-7), 0, FrameCount - 1);

    public static Box2 TextureBounds(Vector2i size)
    => Box2.FromDimensions(new Vector2(-AnchorPixels.X, AnchorPixels.Y - size.Y) / PixelsPerTile,
        new Vector2(size.X, size.Y) / PixelsPerTile);

}
