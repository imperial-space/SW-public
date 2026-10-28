using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossRainCrystalVisuals
{
    public const double WarningSeconds = 3;
    public const double FormationSeconds = 0.8;
    public const double FallSeconds = 0.16;
    public const float ShatterSeconds = 1.2f;
    public const double FrameSeconds = 0.02;
    public const float PixelsPerTile = 48;
    public const string State = "animation";
    public static bool IsCrystal(BossTelegraphPattern pattern) => pattern is BossTelegraphPattern.CrystalRainSmall or BossTelegraphPattern.CrystalRain;
    public static ResPath? Texture(BossTelegraphStylePrototype style, BossTelegraphPattern pattern, bool impact) => (pattern, impact) switch
    {
        (BossTelegraphPattern.CrystalRainSmall, false) => style.SmallCrystalWarningTexture,
        (BossTelegraphPattern.CrystalRainSmall, true) => style.SmallCrystalShatterTexture,
        (BossTelegraphPattern.CrystalRain, false) => style.LargeCrystalWarningTexture,
        (BossTelegraphPattern.CrystalRain, true) => style.LargeCrystalShatterTexture,
        _ => null
    };
    public static Vector2 Center(BossTelegraphPattern pattern, Vector2i origin)
    {
        var offset = pattern == BossTelegraphPattern.CrystalRainSmall ? 1f : 0.5f;
        return new Vector2(origin.X + offset, origin.Y + offset);
    }

    public static Vector2 Anchor(bool impact) => new(80, 304);
    public static double WarningPlayback(double elapsed, double duration)
    {
        if (!double.IsFinite(elapsed) || !double.IsFinite(duration) || duration <= 0)
            return 0;
        elapsed = Math.Clamp(elapsed, 0, duration);
        var fall = Math.Min(FallSeconds, duration);
        var formation = Math.Min(FormationSeconds, duration - fall);
        var fallStart = duration - fall;
        if (elapsed >= fallStart)
            return WarningSeconds - FallSeconds + FallSeconds * (elapsed - fallStart) / fall;
        if (elapsed < formation)
            return FormationSeconds * elapsed / formation;
        var hover = fallStart - formation;
        return hover > 0 ? FormationSeconds + (WarningSeconds - FallSeconds - FormationSeconds) * (elapsed - formation) / hover : FormationSeconds;
    }

    public static (bool Impact, double Elapsed) Playback(double elapsed, double duration, bool impact) => !impact && duration > 0 && elapsed >= duration ? (true, elapsed - duration) : (impact, elapsed);
    public static int FrameIndex(double elapsed, double duration, bool impact)
    {
        var playback = impact ? elapsed : WarningPlayback(elapsed, duration);
        if (!double.IsFinite(playback))
            return 0;
        return (int)Math.Clamp(Math.Floor(playback / FrameSeconds + 1e-7), 0, impact ? 59 : 149);
    }
}
