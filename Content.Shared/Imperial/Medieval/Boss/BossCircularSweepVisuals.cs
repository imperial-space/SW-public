using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossCircularSweepVisuals
{
    public const float DurationSeconds = BossCleaveVisuals.DurationSeconds;
    public const float StartAngle = MathF.PI * 261 / 180;
    public const float SweepAngle = MathF.PI * 342 / 180;
    public const float FeatherAngle = MathF.PI * 3 / 180;

    public static bool IsCircular(BossTelegraphPattern pattern)
        => pattern is BossTelegraphPattern.Rejection or BossTelegraphPattern.Invitation;

    public static bool IsClose(BossTelegraphPattern pattern, bool secondPhase)
        => pattern == BossTelegraphPattern.Rejection ? !secondPhase : secondPhase;

    public static ResPath? Texture(BossTelegraphStylePrototype style, BossTelegraphPattern pattern, bool secondPhase)
        => !IsCircular(pattern) ? null : IsClose(pattern, secondPhase) ? style.CloseSweepTexture : style.DonutSweepTexture;
}
