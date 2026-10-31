using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossBrazierAnimation
{
    public const int FrameCount = 32;
    public const float FrameSeconds = .2f;
    public const double LoopSeconds = 6.4;
    private static readonly int[] CornerOffsets = [0, 7, 15, 24];
    public static int CornerOffset(int corner) => CornerOffsets[Math.Clamp(corner, 0, 3)];
    public static float TimeAt(BossBrazierAnimationComponent animation, TimeSpan now) => (float)((Math.Max(0, (now - animation.StartTime).TotalSeconds) + animation.PhaseOffsetFrames * .2) % LoopSeconds);
}
