using System.Numerics;
using System.Linq;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossConstellationVisuals
{
    public const float DurationSeconds = 1.74f;
    public const double FrameSeconds = 0.03;
    public const int FrameCount = 58;
    public const float EndpointExtension = 0.625f;

    public static ResPath? Texture(BossTelegraphStylePrototype style, BossTelegraphPattern pattern, int halfSize)
        => halfSize > 0 && BossTilePatterns.IsConstellation(pattern) ? style.ConstellationBeam?.Sprite : null;

    public static bool IsBlue(BossTelegraphPattern pattern, bool secondPhase)
        => (pattern == BossTelegraphPattern.BlueFirst) != secondPhase;

    public static string State(BossTelegraphPattern pattern, bool secondPhase)
        => IsBlue(pattern, secondPhase) ? "blue" : "purple";

    public static string BeamState(BossTelegraphPattern pattern, bool secondPhase, int width)
        => State(pattern, secondPhase) + "-" + width;

    public static int FrameIndex(double elapsed)
        => !double.IsFinite(elapsed) ? 0 : (int)Math.Clamp(Math.Floor(elapsed / FrameSeconds + 1e-7), 0, FrameCount - 1);

    public static IEnumerable<BossBeamPlacement> Beams(BossTelegraphPattern pattern, bool secondPhase, int halfSize)
    {
        if (halfSize < 1 || !BossTilePatterns.IsConstellation(pattern))
            yield break;

        var start = -halfSize - EndpointExtension;
        var end = halfSize + 1 + EndpointExtension;
        foreach (var band in BossTilePatterns.ConstellationBeams(IsBlue(pattern, secondPhase), halfSize))
        {
            var center = band.Start + band.Width / 2f;
            yield return new(new(start, center), new(end, center), band.Width);
            yield return new(new(center, start), new(center, end), band.Width);
        }
    }

    public static Box2[] AllowedAreas(IEnumerable<Vector2i> danger, int halfSize)
    {
        var areas = new List<Box2>();
        foreach (var row in danger.Distinct().GroupBy(tile => tile.Y))
        {
            var columns = row.Select(tile => tile.X).Order().ToArray();
            var start = columns[0];
            var end = start;
            foreach (var column in columns.Skip(1))
            {
                if (column == end + 1)
                    end = column;
                else
                {
                    areas.Add(new(start, row.Key, end + 1, row.Key + 1));
                    start = end = column;
                }
            }
            areas.Add(new(start, row.Key, end + 1, row.Key + 1));
        }

        var outside = halfSize + 16;
        areas.Add(new(-outside, -outside, -halfSize, outside));
        areas.Add(new(halfSize + 1, -outside, outside, outside));
        areas.Add(new(-halfSize, -outside, halfSize + 1, -halfSize));
        areas.Add(new(-halfSize, halfSize + 1, halfSize + 1, outside));
        return areas.ToArray();
    }

    public static float ScorchOpacity(double elapsed)
    {
        var age = FrameIndex(elapsed) * 0.02;
        var opacity = age < 0.08 ? age / 0.08 : age < 0.26 ? 1 : Math.Pow(1 - (age - 0.26) / 0.9, 1.35);
        return age >= 1.14 ? 0 : (float)(Math.Round(36 * opacity) / 255);
    }
}
