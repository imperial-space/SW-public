using System.Numerics;
using Content.Shared.Hands.Components;
using Robust.Shared.Maths;

namespace Content.Shared.Imperial.Medieval.Weapons;

public static class MeleeSwingVisuals
{
    public const string LeftToRightAnimation = "CustomMeleeSwingLeftToRight";
    public const string RightToLeftAnimation = "CustomMeleeSwingRightToLeft";

    public static bool IsValid(MeleeSwingStylePrototype style)
        => style.FrameCount > 0 && float.IsFinite(style.FrameSeconds) && style.FrameSeconds > 0 &&
           float.IsFinite(style.ReferenceSwingSeconds) && style.ReferenceSwingSeconds > 0 &&
           float.IsFinite(style.PlaybackSpeed) && style.PlaybackSpeed > 0 &&
           float.IsFinite(style.ReferenceRadiusPixels) && style.ReferenceRadiusPixels > 0 &&
           float.IsFinite(style.ReachScale) && style.ReachScale > 0 &&
           double.IsFinite(style.Arc.Theta) && style.Arc.Theta > 0 && style.Arc.Theta <= Math.Tau;

    public static bool IsSwingAnimation(string? animation)
        => animation is LeftToRightAnimation or RightToLeftAnimation;

    public static string SwingMarker(bool leftToRight)
        => leftToRight ? LeftToRightAnimation : RightToLeftAnimation;

    public static string SwingState(MeleeSwingStylePrototype style, bool leftToRight)
        => leftToRight ? style.LeftState : style.RightState;

    public static float SwingPlaybackScale(MeleeSwingStylePrototype style, float normalSeconds)
        => (float.IsFinite(normalSeconds) && normalSeconds > 0 ? normalSeconds : style.ReferenceSwingSeconds) /
           style.ReferenceSwingSeconds / style.PlaybackSpeed;

    public static int SwingFrame(MeleeSwingStylePrototype style, float elapsed, float normalSeconds)
        => Math.Clamp((int)(elapsed / (style.FrameSeconds * SwingPlaybackScale(style, normalSeconds))), 0, style.FrameCount - 1);

    public static float WeaponSwingSeconds(MeleeSwingStylePrototype style, float normalSeconds)
        => (float.IsFinite(normalSeconds) && normalSeconds > 0 ? normalSeconds : style.ReferenceSwingSeconds) / style.PlaybackSpeed;

    public static float WeaponSwingDuration(MeleeSwingStylePrototype style, float normalSeconds)
        => style.Duration * SwingPlaybackScale(style, normalSeconds);

    public static float SwingDrawRadius(MeleeSwingStylePrototype style, float reach) => reach * style.ReachScale;

    public static Angle LeadingAngle(MeleeSwingStylePrototype style, float elapsed, float normalSeconds, bool leftToRight)
    {
        var time = (SwingFrame(style, elapsed, normalSeconds) + 0.5f) * style.FrameSeconds;
        var progress = Math.Clamp(time / style.ReferenceSwingSeconds, 0, 1);
        if (style.Motion.Count > 0)
        {
            progress = style.Motion[0].Progress;
            for (var index = 1; index < style.Motion.Count; index++)
            {
                var previous = style.Motion[index - 1];
                var current = style.Motion[index];
                var amount = Math.Clamp((time - previous.Time) / Math.Max(0.0001f, current.Time - previous.Time), 0, 1);
                progress = previous.Progress + (current.Progress - previous.Progress) * amount;
                if (time <= current.Time) break;
            }
        }
        var arc = (float)style.Arc;
        return new Angle((leftToRight ? 1 : -1) * (arc / 2 - arc * progress));
    }

    public static Angle WeaponRotation(MeleeSwingStylePrototype style, Angle aim, float elapsed, float normalSeconds, bool leftToRight)
    {
        var blade = leftToRight ? new Angle(Math.PI - style.WeaponAngle.Theta) : style.WeaponAngle; // mirror the blade for the return swing
        return aim + LeadingAngle(style, elapsed, normalSeconds, leftToRight) - blade;
    }

    public static float WeaponAlpha(MeleeSwingStylePrototype style, float elapsed, float normalSeconds)
    {
        var cut = style.FadeStart * SwingPlaybackScale(style, normalSeconds);
        var end = WeaponSwingDuration(style, normalSeconds);
        return end <= cut ? (elapsed < end ? 1 : 0) : Math.Clamp(1 - (elapsed - cut) / (end - cut), 0, 1);
    }

    public static (string? Front, string? Rear) HeldStates(MeleeSwingStylePrototype style, HandLocation hand, bool wielded)
        => wielded ? (style.WieldedFront, style.WieldedRear) : hand == HandLocation.Left
            ? (style.LeftFront, style.LeftRear) : (style.RightFront, style.RightRear);

    public static float ChargeRatio(TimeSpan held, float maximumSeconds)
        => !float.IsFinite(maximumSeconds) || maximumSeconds <= 0 ? 0 : Math.Clamp((float)held.TotalSeconds / maximumSeconds, 0, 1);

    public static float SwingScale(float ratio, float maximum)
        => 1 + Math.Clamp(ratio, 0, 1) * ((float.IsFinite(maximum) ? Math.Max(1, maximum) : 1) - 1);

    public static Angle SwingTextureRotation(Vector2 localAim, Angle parentRotation)
        => parentRotation.RotateVec(localAim).ToAngle();

    public static Box2 TextureBounds(Vector2i size, Vector2 pivot, float pixelsPerUnit, float scale)
        => Box2.FromDimensions(new Vector2(-pivot.X, pivot.Y - size.Y) / pixelsPerUnit * scale,
            new Vector2(size.X, size.Y) / pixelsPerUnit * scale);
}
