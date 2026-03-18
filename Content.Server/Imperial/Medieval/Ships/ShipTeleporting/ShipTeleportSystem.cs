using Content.Server.Administration.Logs;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
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
        var mapScale = _cfg.GetCVar(ShipsCCVars.MapScale);
        var tpRange = _cfg.GetCVar(ShipsCCVars.TeleportRange);
        var tpDist = mapScale + tpRange;
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

            if (Math.Abs(newcoords.X) > tpDist)
            {
                if (newcoords.X > 0)
                {
                    x += 1;
                    newcoords.X = -tpDist;
                }
                else
                {
                    x -= 1;
                    newcoords.X = tpDist;
                }
            }

            if (Math.Abs(newcoords.Y) > tpDist)
            {
                if (newcoords.Y > 0)
                {
                    y += 1;
                    newcoords.Y = -tpDist;
                }

                else
                {
                    y -= 1;
                    newcoords.Y = tpDist;
                }
            }

            var mapId = seematrix.GetCell(x,y).SeaId;
            if (mapId == new MapId(-1))
            {
                EnsureComp<ShipDrowningComponent>(ship, out var comp);
                comp.DrownLevel += (int)Math.Abs(coords.Position.X) + (int)Math.Abs(coords.Position.Y);
            }

            var nmapcoords = new MapCoordinates(newcoords, mapId);

            _transform.SetMapCoordinates(ship, nmapcoords);
            break;
        }


    }
}
