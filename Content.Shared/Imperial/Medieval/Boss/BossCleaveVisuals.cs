namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossCleaveVisuals
{
    public const float ArcRadians = MathF.PI * 7 / 6;
    public const float ExpandSeconds = 0.4f;
    public const float TrailSeconds = 0.30f;
    public const float DurationSeconds = ExpandSeconds + TrailSeconds;
    public const float StartRadiusRatio = 0.86f;
    public const float InitialReachRatio = 0.95f;
    public const float AppearSeconds = 0.025f / 3;
    public const float CornerPadding = 0.02f;

    public static bool IsCleave(BossTelegraphPattern pattern)
        => pattern is BossTelegraphPattern.Sinistra or BossTelegraphPattern.Dextra or
            BossTelegraphPattern.Frons or BossTelegraphPattern.Tergum;

    public static float Rotation(BossTelegraphPattern pattern, int facing)
    {
        var turns = pattern switch
        {
            BossTelegraphPattern.Sinistra => 1,
            BossTelegraphPattern.Dextra => -1,
            BossTelegraphPattern.Tergum => 2,
            _ => 0
        };
        return (facing + turns) * MathF.PI / 2;
    }

    public static float Progress(float elapsed)
    {
        var progress = Math.Clamp((elapsed - AppearSeconds) / (ExpandSeconds - AppearSeconds), 0, 1);
        return progress * progress * (3 - 2 * progress);
    }

    public static float Radius(float initialRadius, float reach, float elapsed)
        => initialRadius + (reach - initialRadius) * Progress(elapsed);

    public static float InitialRadius(int halfSize) => (halfSize + 0.5f) * InitialReachRatio * StartRadiusRatio;

    public static float StrokeAlpha(float fraction, float elapsed)
        => 0.04f + 0.96f * MathF.Pow(Math.Clamp(fraction, 0, 1),
            1.5f + 0.7f * Math.Clamp(elapsed / ExpandSeconds, 0, 1));

    public static float AppearanceAlpha(float elapsed) => Math.Clamp(elapsed / AppearSeconds, 0, 1);

    public static float TextureAlpha(float elapsed)
    {
        if (elapsed < 0 || elapsed >= DurationSeconds) return 0;
        var fade = Math.Clamp((DurationSeconds - elapsed) / TrailSeconds, 0, 1);
        return AppearanceAlpha(elapsed) * fade * fade * (3 - 2 * fade);
    }

    public static IEnumerable<(float Time, float Alpha, bool Leading)> Samples(float elapsed)
    {
        if (elapsed < 0 || elapsed >= DurationSeconds) yield break;
        for (var index = 8; index >= 1; index--)
        {
            var age = TrailSeconds * index / 9;
            var sample = elapsed - age;
            if (sample < 0 || sample > ExpandSeconds) continue;
            var fade = 1 - age / TrailSeconds;
            yield return (sample, 0.18f * fade * fade * AppearanceAlpha(sample), false);
        }
        var leadingFade = Math.Clamp(1 - (elapsed - ExpandSeconds) / (TrailSeconds * 5 / 12), 0, 1);
        if (leadingFade > 0)
            yield return (Math.Min(elapsed, ExpandSeconds), leadingFade * AppearanceAlpha(elapsed), true);
    }
}
