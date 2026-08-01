using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

public sealed class IslandRing
{
    public readonly float Inner;
    public readonly float Outer;
    public IslandRing(float inner, float outer) { Inner = inner; Outer = outer; }
}

public readonly struct IslandPlacement
{
    public readonly Vector2 Pos;
    public readonly ResPath Path;
    public readonly float Radius;
    public IslandPlacement(Vector2 pos, ResPath path, float radius) { Pos = pos; Path = path; Radius = radius; }
}

public sealed class IslandSpatialGrid
{
    private readonly float _cell;
    private float _maxR;
    private readonly Dictionary<long, List<IslandPlacement>> _cells = new();

    public IslandSpatialGrid(float cellSize) { _cell = MathF.Max(1f, cellSize); }

    private long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    private (int, int) CellOf(Vector2 p) =>
        ((int)MathF.Floor(p.X / _cell), (int)MathF.Floor(p.Y / _cell));

    public void Add(IslandPlacement isle)
    {
        _maxR = MathF.Max(_maxR, isle.Radius);
        var (cx, cy) = CellOf(isle.Pos);
        var k = Key(cx, cy);
        if (!_cells.TryGetValue(k, out var list)) { list = new(); _cells[k] = list; }
        list.Add(isle);
    }

    public bool Conflicts(Vector2 p, float radius, float gap, float minimumCenterDistance = 0f)
    {
        var maximumConflictDistance = MathF.Max(radius + _maxR + gap, minimumCenterDistance);
        var range = (int)MathF.Ceiling(maximumConflictDistance / _cell);
        var (cx, cy) = CellOf(p);
        for (var dx = -range; dx <= range; dx++)
        for (var dy = -range; dy <= range; dy++)
            if (_cells.TryGetValue(Key(cx + dx, cy + dy), out var list))
                foreach (var other in list)
                {
                    var min = MathF.Max(radius + other.Radius + gap, minimumCenterDistance);
                    if (Vector2.DistanceSquared(p, other.Pos) < min * min)
                        return true;
                }
        return false;
    }
}

public sealed class IslandBridsonGenerator
{
    private readonly float _gap;
    private readonly int _maxCandidatesPerPoint;

    public IslandBridsonGenerator(float gap, int maxCandidatesPerPoint = 30) { _gap = gap; _maxCandidatesPerPoint = maxCandidatesPerPoint; }

    public List<IslandPlacement> Generate(
        IslandRing ring,
        List<(ResPath Path, float Radius)> islands,
        int targetCount,
        IslandSpatialGrid grid,
        Random rng)
    {
        var result = new List<IslandPlacement>();
        if (islands.Count == 0 || targetCount <= 0)
            return result;

        var remaining = Shuffle(islands, rng);
        if (remaining.Count > targetCount)
            remaining.RemoveRange(targetCount, remaining.Count - targetCount);

        var distributionDistance = CalculateDistributionDistance(ring, remaining.Count);
        var maximumRadius = 0f;
        foreach (var (_, radius) in remaining)
            maximumRadius = MathF.Max(maximumRadius, radius);

        var ringGrid = new IslandSpatialGrid(MathF.Max(distributionDistance, maximumRadius + _gap));
        var active = new List<IslandPlacement>();

        while (remaining.Count > 0)
        {
            if (active.Count == 0)
            {
                if (!TryCreateSeed(
                        ring,
                        remaining,
                        grid,
                        ringGrid,
                        distributionDistance,
                        rng,
                        out var seed,
                        out var seedIndex))
                    break;

                remaining.RemoveAt(seedIndex);
                AddPlacement(seed, active, result, grid, ringGrid);
                continue;
            }

            var idx = rng.Next(active.Count);
            var origin = active[idx];

            if (!TryCreateAround(
                    origin,
                    ring,
                    remaining,
                    grid,
                    ringGrid,
                    distributionDistance,
                    rng,
                    out var placement,
                    out var remainingIndex))
            {
                active.RemoveAt(idx);
                continue;
            }

            remaining.RemoveAt(remainingIndex);
            AddPlacement(placement, active, result, grid, ringGrid);
        }

        return result;
    }

    private bool TryCreateSeed(
        IslandRing ring,
        List<(ResPath Path, float Radius)> remaining,
        IslandSpatialGrid grid,
        IslandSpatialGrid ringGrid,
        float distributionDistance,
        Random rng,
        out IslandPlacement placement,
        out int remainingIndex)
    {
        var attemptsPerIsland = Math.Max(64, _maxCandidatesPerPoint * 4);
        foreach (var index in ShuffledIndices(remaining.Count, rng))
        {
            var (path, radius) = remaining[index];
            for (var attempt = 0; attempt < attemptsPerIsland; attempt++)
            {
                var position = RandomInRing(ring, rng);
                if (grid.Conflicts(position, radius, _gap))
                    continue;
                if (ringGrid.Conflicts(position, radius, _gap, distributionDistance))
                    continue;

                placement = new IslandPlacement(position, path, radius);
                remainingIndex = index;
                return true;
            }
        }

        placement = default;
        remainingIndex = -1;
        return false;
    }

    private bool TryCreateAround(
        IslandPlacement origin,
        IslandRing ring,
        List<(ResPath Path, float Radius)> remaining,
        IslandSpatialGrid grid,
        IslandSpatialGrid ringGrid,
        float distributionDistance,
        Random rng,
        out IslandPlacement placement,
        out int remainingIndex)
    {
        foreach (var index in ShuffledIndices(remaining.Count, rng))
        {
            var (path, radius) = remaining[index];
            var minimumDistance = MathF.Max(origin.Radius + radius + _gap, distributionDistance);

            for (var attempt = 0; attempt < _maxCandidatesPerPoint; attempt++)
            {
                var position = SampleAnnulus(origin.Pos, minimumDistance, minimumDistance * 2f, rng);
                if (!FitsInRing(position, ring))
                    continue;
                if (grid.Conflicts(position, radius, _gap))
                    continue;
                if (ringGrid.Conflicts(position, radius, _gap, distributionDistance))
                    continue;

                placement = new IslandPlacement(position, path, radius);
                remainingIndex = index;
                return true;
            }
        }

        placement = default;
        remainingIndex = -1;
        return false;
    }

    private static void AddPlacement(
        IslandPlacement placement,
        List<IslandPlacement> active,
        List<IslandPlacement> result,
        IslandSpatialGrid grid,
        IslandSpatialGrid ringGrid)
    {
        active.Add(placement);
        result.Add(placement);
        grid.Add(placement);
        ringGrid.Add(placement);
    }

    private static float CalculateDistributionDistance(IslandRing ring, int targetCount)
    {
        var area = MathF.PI * MathF.Max(0f, ring.Outer * ring.Outer - ring.Inner * ring.Inner);
        return MathF.Sqrt(area / targetCount);
    }

    private static bool FitsInRing(Vector2 p, IslandRing ring)
    {
        var d = p.Length();
        return d >= ring.Inner && d <= ring.Outer;
    }

    private static Vector2 RandomInRing(IslandRing ring, Random rng)
    {
        var u = rng.NextSingle();
        var r = MathF.Sqrt(ring.Inner * ring.Inner + u * (ring.Outer * ring.Outer - ring.Inner * ring.Inner));
        var a = rng.NextSingle() * MathF.Tau;
        return new Vector2(r * MathF.Cos(a), r * MathF.Sin(a));
    }

    private static Vector2 SampleAnnulus(Vector2 center, float inner, float outer, Random rng)
    {
        var u = rng.NextSingle();
        var r = MathF.Sqrt(inner * inner + u * (outer * outer - inner * inner));
        var a = rng.NextSingle() * MathF.Tau;
        return center + new Vector2(r * MathF.Cos(a), r * MathF.Sin(a));
    }

    private static List<T> Shuffle<T>(List<T> source, Random rng)
    {
        var list = new List<T>(source);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    private static List<int> ShuffledIndices(int count, Random rng)
    {
        var indices = new List<int>(count);
        for (var i = 0; i < count; i++)
            indices.Add(i);

        for (var i = indices.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
    }
}
