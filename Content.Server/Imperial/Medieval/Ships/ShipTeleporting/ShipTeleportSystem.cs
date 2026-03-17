using Content.Server.Administration.Logs;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.ShipTeleporting;

/// <summary>
/// This handles...
/// </summary>
public sealed class ShipTeleportSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly WaveSystem _wave = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    private TimeSpan _nextCheckTime;
    /// <inheritdoc/>
    public override void Initialize()
    {

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WaveDelay));

            foreach (var seaComponent in EntityManager.EntityQuery<SeaComponent>())
            {

                var sea = seaComponent.Owner;

                var entities = new HashSet<Entity<MapGridComponent>>();
                _lookup.GetEntitiesOnMap(_transform.GetMapId(sea), entities);
                foreach (var shipComp in entities)
                {
                    var ship = shipComp.Owner;


                    var coords = _transform.GetMapCoordinates(ship);
                    if (Math.Abs(coords.X) > 250 || Math.Abs(coords.Y) > 250)
                    {
                        TeleportShip(ship, coords);
                    }
                }
            }
        }
    }

    private void TeleportShip(EntityUid ship, MapCoordinates coords)
    {
        var newcoords = coords.Position;
        foreach (var magicBarrier in EntityManager.EntityQuery<MagicBarrierComponent>())
        {
            var seematrix = magicBarrier.SeaMatrix;
            if ( seematrix is null)
                continue;
            var seamap = seematrix.FoundSell(coords.MapId, seematrix);
            if (seamap is null)
                continue;

            var (x, y) = seamap.Value;

            if (Math.Abs(newcoords.X) > 250)
            {
                if (newcoords.X > 0)
                    x += 1;
                else
                {
                    x -= 1;
                }
                newcoords.X *= -1;
            }

            if (Math.Abs(newcoords.Y) > 250)
            {
                if (newcoords.Y > 0)
                    y += 1;
                else
                {
                    y -= 1;
                }
                newcoords.Y *= -1;
            }

            var mapId = seematrix.GetCell(x,y).SeaId;

            var nmapcoords = new MapCoordinates(newcoords, mapId);

            _transform.SetMapCoordinates(ship, nmapcoords);

        }


    }
}
