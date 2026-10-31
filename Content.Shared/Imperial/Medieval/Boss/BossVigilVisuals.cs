using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossVigilVisuals
{
    public const float MarkSeconds = 1.76f;
    public const float MarkFrameSeconds = .04f;
    public const int MarkFrames = 44;
    public const float HoldSeconds = 1;
    public const float LightReturnSeconds = 1.5f;
    public const float BodyReturnDelaySeconds = .4f;
    public const float BodyReturnSeconds = .8f;
    public const float RecoverySeconds = HoldSeconds + LightReturnSeconds;
    public const float AshSeconds = .8f;
    public const float AshFrameSeconds = .05f;
    public static readonly Vector2 MarkAnchorPixels = new(32, 40);

    public static readonly Vector2 VictimAnchorOffset = new(0, -10f / 32);

    public static bool Enabled(BossTelegraphStylePrototype style, BossTelegraphPattern pattern)
        => BossTilePatterns.IsBlackout(pattern) && style.VigilMarkTexture != null && style.VigilAshTexture != null;

    public static int MarkFrame(double age)
        => (int)Math.Clamp(Math.Floor(age / MarkFrameSeconds + 1e-6), 0, MarkFrames - 1);

    public static Box2 MarkBounds(Vector2i size)
        => new(-MarkAnchorPixels.X / 32, (MarkAnchorPixels.Y - size.Y) / 32,
            (size.X - MarkAnchorPixels.X) / 32, MarkAnchorPixels.Y / 32);

    public static float ReturnProgress(BossVigilComponent visual, TimeSpan now)
    {
        if (!visual.Resolved) return 0;
        var t = (float)Math.Clamp(((now - visual.ResolveTime).TotalSeconds - HoldSeconds) / LightReturnSeconds, 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static int AshFrame(BossVigilComponent visual, TimeSpan now)
    {
        var elapsed = (now - visual.StartTime).TotalSeconds;
        if (elapsed < 0) return -1;
        if (elapsed < AshSeconds) return (int)Math.Clamp(Math.Floor(elapsed / AshFrameSeconds + 1e-6), 0, 15);

        return -1;
    }

    public static float BodyOpacity(BossVigilComponent visual, TimeSpan now)
    {
        if (!visual.Resolved) return 0;
        var t = (float)Math.Clamp(((now - visual.ResolveTime).TotalSeconds - HoldSeconds - BodyReturnDelaySeconds) /
                                 BodyReturnSeconds, 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static bool BodyVisible(BossVigilComponent visual, TimeSpan now)
        => BodyOpacity(visual, now) > 0;
}
