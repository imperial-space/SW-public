using Content.Server.Shuttles.Components;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Wind;

/// <summary>
/// This handles...
/// </summary>
public sealed class ServerWindSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

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
            _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WindChangeTime));
            RandomiseVind();
        }
    }


    private void RandomiseVind()
    {
        // ветерок сила
        var windForce = _cfg.GetCVar(ShipsCCVars.StormLevel);
        var countShips = FindShips();

        if (windForce <= 0+countShips)
            windForce += _random.Next(0, 2);
        else if (windForce >= 2 + countShips || countShips >= 10)
            windForce -= _random.Next(0, 2);
        else
            windForce += _random.Next(-1, 2);
        _cfg.SetCVar(ShipsCCVars.WindPower, windForce);

        // ветерок направление
        var windAngle = _cfg.GetCVar(ShipsCCVars.WindRotation);
        windAngle += _random.Next(-1, 2)*5; // ±5 градусов за шаг
        if (Math.Abs(windAngle) > 360)
            windAngle = 0;
        else if (windAngle < 0)
            windAngle += 360;

        _cfg.SetCVar(ShipsCCVars.WindRotation, windAngle);
    }

    private int FindShips()
    {
        var count = 0;
        foreach (var seaComp in EntityManager.EntityQuery<SeaComponent>())
        {
            if (seaComp.Disabled)
                continue;
            var mapUid =  seaComp.Owner;
            MapId mapId = new MapId(mapUid.Id);

            var ships = new HashSet<Entity<ShuttleComponent>>();
            _lookup.GetEntitiesOnMap(mapId, ships);
            count += ships.Count;

        }
        return count;
    }
}
