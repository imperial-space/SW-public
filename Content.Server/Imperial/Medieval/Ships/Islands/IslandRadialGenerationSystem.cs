using System.Numerics;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

public sealed class IslandRadialGenerationSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const int MaxConsecutiveFails = 10;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IslandRadialGenerationComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, IslandRadialGenerationComponent comp, MapInitEvent args)
    {
        var mapId = Transform(uid).MapID;
        if (mapId == MapId.Nullspace)
            return;

        SpawnIslands(mapId, comp);
    }

    public void SpawnIslands(MapId mapId, IslandRadialGenerationComponent config)
    {
        var spawnedIslands = new List<(Vector2 Center, float Radius)>();

        SpawnTier(mapId, config.LowIslands, config.LowIslandMinRange, config.MediumIslandMinRange, config.InterIslandsThreshold, spawnedIslands);
        SpawnTier(mapId, config.MediumIslands, config.MediumIslandMinRange, config.HighIslandMinRange, config.InterIslandsThreshold, spawnedIslands);
        SpawnTier(mapId, config.HighIslands, config.HighIslandMinRange, config.HighIslandMaxRange, config.InterIslandsThreshold, spawnedIslands);
    }

    private void SpawnTier(
        MapId mapId,
        List<ResPath> islands,
        float innerRadius,
        float outerRadius,
        float threshold,
        List<(Vector2 Center, float Radius)> spawnedIslands)
    {
        var consecutiveFails = 0;

        foreach (var islandPath in islands)
        {
            if (consecutiveFails >= MaxConsecutiveFails)
                return;

            var placed = false;
            while (!placed)
            {
                if (consecutiveFails >= MaxConsecutiveFails)
                    return;

                var candidate = PickRandomPoint(innerRadius, outerRadius);
                if (IsTooClose(candidate, spawnedIslands, threshold))
                {
                    consecutiveFails++;
                    continue;
                }

                if (!_mapLoader.TryLoadGrid(mapId, islandPath, out var grid, offset: candidate))
                    break;

                var radius = grid!.Value.Comp.LocalAABB.Extents.Length();
                spawnedIslands.Add((candidate, radius));
                consecutiveFails = 0;
                placed = true;
            }
        }
    }

    private Vector2 PickRandomPoint(float innerRadius, float outerRadius)
    {
        var angle = _random.NextFloat() * MathF.Tau;
        var distance = innerRadius + _random.NextFloat() * (outerRadius - innerRadius);
        return new Vector2(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance);
    }

    private static bool IsTooClose(Vector2 candidate, List<(Vector2 Center, float Radius)> existing, float threshold)
    {
        foreach (var (center, radius) in existing)
        {
            if ((candidate - center).Length() < radius + threshold)
                return true;
        }

        return false;
    }
}
