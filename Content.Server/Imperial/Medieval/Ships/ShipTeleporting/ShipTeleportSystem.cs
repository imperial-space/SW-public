using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
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
                var ship = seaComponent.Owner;

                var coords = _transform.GetMapCoordinates(ship);
                if (Math.Abs(coords.X) > 250 || Math.Abs(coords.Y) > 250)
                {
                    TeleportShip(ship, coords);
                }

            }
        }
    }

    private void TeleportShip(EntityUid ship, MapCoordinates coords)
    {
        var newcoords = coords.Position;
        if (Math.Abs(newcoords.X) > 250)
            newcoords.X *= -1;
        if (Math.Abs(newcoords.Y) > 250)
            newcoords.Y *= -1;
        var nmapcoords = new MapCoordinates(newcoords, coords.MapId);
        _transform.SetMapCoordinates(ship, nmapcoords);
    }
}
