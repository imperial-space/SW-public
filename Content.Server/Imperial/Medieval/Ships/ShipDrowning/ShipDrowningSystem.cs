using System.Linq;
using Content.Server.Administration.Commands;
using Content.Server.Imperial.Medieval.Ships.PlayerDrowning;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.ShipDrowning;

/// <summary>
/// This handles...
/// </summary>
public sealed class ShipDrowningSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly WaveSystem _wave = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;

    private const float DefaultReloadTimeSeconds = 10f;

    private TimeSpan _nextCheckTime;
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

            foreach (var component in EntityManager.EntityQuery<ShipDrowningComponent>())
            {
                var ship = component.Owner;

                if (component.DrownLevel > component.DrownMaxLevel)
                {
                    if (component.DrownLevel > component.DrownMaxLevel * 10)
                    {
                        _entityManager.DeleteEntity(ship);
                        return;
                    }
                    EnsureComp<DrownerComponent>(ship);
                    return;
                }
                if (HasComp<DrownerComponent>(ship))
                    RemComp<DrownerComponent>(ship);

                if (!TryComp<MapGridComponent>(ship, out var mapGrid))
                    return;
                var allTilesEnumerator = _map.GetAllTilesEnumerator(ship, mapGrid);

                var brokenTilesCount = 0;
                var allTilesCount = 0;

                while (allTilesEnumerator.MoveNext(out var tile))
                {
                    allTilesCount++;
                    _tileDefinitionManager.TryGetDefinition("FloorBrokenWoodDDD", out var tileDef);
                    if (tileDef is null || tile.Value.Tile.TypeId != tileDef.TileId)
                        continue;
                    brokenTilesCount += tile.Value.Tile.Variant;
                }

                component.DrownLevel += brokenTilesCount;
                component.DrownMaxLevel = allTilesCount * 100;
            }

        }

    }
}
