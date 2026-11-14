using System.Linq;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossTilePatterns
{
    public static readonly Vector2i[] Platform = GetPlatform(5);

    public static Vector2i[] GetPlatform(int halfSize) => Enumerable.Range(-halfSize, halfSize * 2 + 1)
        .SelectMany(y => Enumerable.Range(-halfSize, halfSize * 2 + 1).Select(x => new Vector2i(x, y))).ToArray();

    public static bool IsPlatform(Vector2i tile) => IsPlatform(tile, 5);
    public static bool IsPlatform(Vector2i tile, int halfSize) => Math.Abs(tile.X) <= halfSize && Math.Abs(tile.Y) <= halfSize;
    public static bool IsTwoPhase(BossTelegraphPattern pattern)
        => pattern is BossTelegraphPattern.Rejection or BossTelegraphPattern.Invitation or
            BossTelegraphPattern.BlueFirst or BossTelegraphPattern.PurpleFirst;
    public static bool IsConstellation(BossTelegraphPattern pattern)
        => pattern is BossTelegraphPattern.BlueFirst or BossTelegraphPattern.PurpleFirst;
    public static bool IsBlackout(BossTelegraphPattern pattern)
        => pattern >= BossTelegraphPattern.BlackoutNorthWest && pattern <= BossTelegraphPattern.BlackoutSouthEast;

    public static Vector2i BlackoutCenter(BossTelegraphPattern pattern, int halfSize = 5) => pattern switch
    {
        BossTelegraphPattern.BlackoutNorthWest => new(1 - halfSize, halfSize - 1),
        BossTelegraphPattern.BlackoutNorthEast => new(halfSize - 1, halfSize - 1),
        BossTelegraphPattern.BlackoutSouthWest => new(1 - halfSize, 1 - halfSize),
        _ => new(halfSize - 1, 1 - halfSize)
    };

    public static int Distance(Vector2i a, Vector2i b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    public static (int Start, int Width)[] ConstellationBeams(bool blue, int halfSize = 5)
    {
        var result = new List<(int Start, int Width)>();
        for (var position = -halfSize; position <= halfSize; position++)
        {
            if (((position % 4 + 4) % 4 < 2) != blue) continue;
            if (result.Count > 0 && result[^1].Start + result[^1].Width == position)
                result[^1] = (result[^1].Start, result[^1].Width + 1);
            else result.Add((position, 1));
        }
        return result.ToArray();
    }
    public static int[] ConstellationLanes(bool blue)
        => ConstellationBeams(blue).SelectMany(beam => Enumerable.Range(beam.Start, beam.Width)).ToArray();
    public static bool ConstellationHits(Vector2i tile, bool blue, int halfSize = 5)
    {
        foreach (var beam in ConstellationBeams(blue, halfSize))
            if (tile.X >= beam.Start && tile.X < beam.Start + beam.Width ||
                tile.Y >= beam.Start && tile.Y < beam.Start + beam.Width)
                return true;
        return false;
    }
    public static Vector2i Rotate(Vector2i tile, int turns) => ((turns % 4 + 4) % 4) switch
    {
        1 => new(-tile.Y, tile.X),
        2 => new(-tile.X, -tile.Y),
        3 => new(tile.Y, -tile.X),
        _ => tile
    };

    public static List<Vector2i> Danger(BossTelegraphPattern pattern, IReadOnlyList<Vector2i> targets,
        bool secondPhase = false, int facing = 0, Vector2i origin = default, int halfSize = 5)
    {

        if (pattern == BossTelegraphPattern.Stack && targets.Count == 0)
            return new();
        return GetPlatform(halfSize).Where(tile =>
        {
            var offset = tile - origin;                                          // measure each tile from the mechanic center
            var local = Rotate(offset, -facing);                                 // test directions relative to the attack facing
            var radius = offset.X * offset.X + offset.Y * offset.Y;              // squared distance for the circular patterns
            return pattern switch
            {
                BossTelegraphPattern.Sinistra => local.X <= 0,
                BossTelegraphPattern.Dextra => local.X >= 0,
                BossTelegraphPattern.Frons => local.Y >= 0,
                BossTelegraphPattern.Tergum => local.Y <= 0,
                BossTelegraphPattern.Rejection => secondPhase ? radius > 9 : radius <= 16,
                BossTelegraphPattern.Invitation => secondPhase ? radius <= 16 : radius > 9,
                BossTelegraphPattern.Cross => Math.Abs(offset.X) <= 1 || Math.Abs(offset.Y) <= 1,
                BossTelegraphPattern.Divide => Math.Abs(offset.X) > 1 && Math.Abs(offset.Y) > 1,
                BossTelegraphPattern.CardinalCone => local.Y < 0 && Math.Abs(local.X) <= -local.Y,
                BossTelegraphPattern.CrystalRain => Distance(tile, origin) <= 1,
                BossTelegraphPattern.CrystalRainSmall => offset.X is >= 0 and <= 1 && offset.Y is >= 0 and <= 1,
                BossTelegraphPattern.Spread => targets.Any(center => Distance(tile, center) <= 2 && tile != center) ||
                    targets.Count(center => center == tile) > 1,
                BossTelegraphPattern.Stack => Distance(tile, targets[0]) > 1,
                BossTelegraphPattern.BlueFirst => ConstellationHits(tile, !secondPhase, halfSize),
                BossTelegraphPattern.PurpleFirst => ConstellationHits(tile, secondPhase, halfSize),
                _ when IsBlackout(pattern) => Distance(tile, BlackoutCenter(pattern, halfSize)) > 1, // leave a safe square around the chosen brazier
                _ => false
            };
        }).ToList();
    }
}
