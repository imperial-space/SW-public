using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossCommunionVisuals
{
    public const float HoldSeconds = 1;
    public const float FadeSeconds = 1.2f;
    public const float DurationSeconds = HoldSeconds + FadeSeconds;
    public const float FeatherTiles = .6f;
    public const float FogSpanTiles = 24;
    public const float LanternEnergy = 10;
    public const float LanternRadius = 2.5f;
    public const float FogImpactOpacity = .52f;
    public const float MinimumBossLightRatio = .12f;
    public static readonly Color LanternColor = Color.FromHex("#EDAA43");

    public static bool Enabled(BossTelegraphStylePrototype style, BossTelegraphPattern pattern)
        => pattern == BossTelegraphPattern.Stack && style.CommunionFogTexture != null && style.CommunionLanternTexture != null;

    public static bool ReplacesLegacyCue(BossTelegraphStylePrototype style, BossActorMarkerKind kind)
        => kind == BossActorMarkerKind.Stack && Enabled(style, BossTelegraphPattern.Stack);

    public static bool UsesFog(BossTelegraphComponent visual)
        => !visual.Preload && (!visual.StoredStrike || visual.Impact);

    public static Box2 SafeBounds(Vector2i tile, int halfSize)
        => new(Math.Max(tile.X - 1, -halfSize), Math.Max(tile.Y - 1, -halfSize),
            Math.Min(tile.X + 2, halfSize + 1), Math.Min(tile.Y + 2, halfSize + 1));

    public static float NativeLightRadius(Box2 refuge)
    => LanternRadius;

    public static float LightMask(Vector2 point, Box2 refuge)
    => Smooth(Math.Min(Math.Min(point.X - refuge.Left, refuge.Right - point.X),
        Math.Min(point.Y - refuge.Bottom, refuge.Top - point.Y)) / .25);

    private static float Smooth(double fraction)
    {
        var t = (float)Math.Clamp(fraction, 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static float Fade(double age)
        => age < 0 ? 0 : 1 - Smooth((age - HoldSeconds) / FadeSeconds);

    public static float FogOpacity(BossTelegraphComponent visual, TimeSpan now)
        => !UsesFog(visual) ? 0 : visual.Impact ? FogImpactOpacity * Fade((now - visual.StartTime).TotalSeconds) :
            now >= visual.EndTime ? 0 : .075f * Smooth(((now - visual.AnimationStartTime).TotalSeconds - .12) / .55);

    public static float BossLightFactor(BossTelegraphComponent visual, TimeSpan now)
        => !UsesFog(visual) ? 1 : visual.Impact ? 1 - (1 - MinimumBossLightRatio) * Fade((now - visual.StartTime).TotalSeconds) :
            now >= visual.EndTime ? 1 : 1 - (1 - MinimumBossLightRatio) * Smooth((now - visual.AnimationStartTime).TotalSeconds / .65);

    public static float LightEnergy(BossTelegraphComponent visual, TimeSpan now)
        => !UsesFog(visual) ? 0 : LanternEnergy * (visual.Impact ? Fade((now - visual.StartTime).TotalSeconds) :
            now >= visual.EndTime ? 0 : Smooth((now - visual.AnimationStartTime).TotalSeconds / .35));

    public static int FogFrame(TimeSpan elapsed)
        => (int)(Math.Max(0, Math.Floor(elapsed.TotalSeconds / .2 + 1e-7)) % 8);

    public static int EdgeFrame(double age)
        => (int)Math.Clamp(Math.Floor(age / .04 + 1e-7), 0, 54);

    public static IEnumerable<(Box2 Bounds, UIBox2 Region)> EdgePatches(Box2 refuge)
    {
        const float pad = .5f;
        yield return (new(refuge.Left - pad, refuge.Top, refuge.Left, refuge.Top + pad), new(160, 160, 168, 168));
        yield return (new(refuge.Right, refuge.Top, refuge.Right + pad, refuge.Top + pad), new(216, 160, 224, 168));
        yield return (new(refuge.Left - pad, refuge.Bottom - pad, refuge.Left, refuge.Bottom), new(160, 216, 168, 224));
        yield return (new(refuge.Right, refuge.Bottom - pad, refuge.Right + pad, refuge.Bottom), new(216, 216, 224, 224));
        var width = (int)MathF.Round(refuge.Width);
        var height = (int)MathF.Round(refuge.Height);
        for (var k = 0; k < width; k++)
        {
            var source = 168 + (width > 1 && k == width - 1 ? 2 : k) * 16;
            var x = refuge.Left + k;
            yield return (new(x, refuge.Top, x + 1, refuge.Top + pad), new(source, 160, source + 16, 168));
            yield return (new(x, refuge.Bottom - pad, x + 1, refuge.Bottom), new(source, 216, source + 16, 224));
        }
        for (var k = 0; k < height; k++)
        {
            var source = 168 + (height > 1 && k == height - 1 ? 2 : k) * 16;
            var y = refuge.Top - k - 1;
            yield return (new(refuge.Left - pad, y, refuge.Left, y + 1), new(160, source, 168, source + 16));
            yield return (new(refuge.Right, y, refuge.Right + pad, y + 1), new(216, source, 224, source + 16));
        }
    }

    public static float FogMask(Vector2 point, Box2 refuge)
    {
        var dx = Math.Max(Math.Max(refuge.Left - point.X, point.X - refuge.Right), 0);
        var dy = Math.Max(Math.Max(refuge.Bottom - point.Y, point.Y - refuge.Top), 0);
        return Smooth(Math.Sqrt(dx * dx + dy * dy) / FeatherTiles);
    }
}
