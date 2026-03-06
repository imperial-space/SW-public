using System.Linq;
using Content.Server.Administration.Commands;
using Content.Server.Imperial.Medieval.Ships.PlayerDrowning;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
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
                var brokenlevel = 0;

                while (allTilesEnumerator.MoveNext(out var tile))
                {
                    allTilesCount++;
                    brokenlevel = 0;
                    foreach (var stage in _wave.Stages)
                    {
                        if (stage.Item2 == tile.Value.Tile.TypeId)
                            brokenTilesCount+= brokenlevel;
                        brokenlevel++;
                    }
                }

                component.DrownLevel += brokenTilesCount;
                component.DrownMaxLevel = allTilesCount * 100;
            }

        }

    }
}
