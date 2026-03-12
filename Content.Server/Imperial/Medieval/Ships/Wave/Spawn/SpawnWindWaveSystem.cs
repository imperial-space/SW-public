using System.Numerics;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Wave.Spawn;

/// <summary>
/// Призыв волн раз в какое то время
/// </summary>
public sealed class SpawnWindWaveSystem : EntitySystem
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
                if (seaComponent.Disabled)
                    continue;
                var sea = seaComponent.Owner;
                var seaMapId = _transform.GetMapId(sea);
                var ships = new HashSet<Entity<ShipDrowningComponent>>();
                _lookup.GetEntitiesOnMap(seaMapId, ships);
                foreach (var shipcomp in ships)
                {
                    var ship = shipcomp.Owner;

                    var waveCount = _random.Next(0, (int)_cfg.GetCVar(ShipsCCVars.StormLevel));
                    var waveCoords = new EntityCoordinates(ship, GenerateWave());
                    Vector2 force;
                    for (var i = 0; i < waveCount; i++)
                    {
                        waveCoords = new EntityCoordinates(ship, GenerateWave());
                        force = waveCoords.Position.Normalized()*_cfg.GetCVar(ShipsCCVars.WaveForce)*-1;
                        _wave.SpawnWave(waveCoords,seaMapId, force);
                    }

                }


            }

        }
    }
    private Vector2 GenerateWave(float radius = 0, float targetAngle = 0, float halfAngle = 3.0235f)
    {
        if (halfAngle == 3.0235f) // я прифигею если вы рандомно сможете получить это число
            halfAngle = _cfg.GetCVar(ShipsCCVars.WaveSpawnAngle)*_cfg.GetCVar(ShipsCCVars.StormLevel);

        halfAngle /= 180;

        if (targetAngle == 0)
            targetAngle = _cfg.GetCVar(ShipsCCVars.WindRotation);
        if (radius == 0)
            radius = _cfg.GetCVar(ShipsCCVars.WaveSpawnRange);

        var u = _random.NextDouble();
        var v = _random.NextDouble();

        var rho = radius * Math.Sqrt(u);

        var phiMin = targetAngle - halfAngle;
        var phiMax = targetAngle + halfAngle;
        var phi = phiMin + (phiMax - phiMin) * v;


        var x = (float)(rho * Math.Cos(phi));
        var y = (float)(rho * Math.Sin(phi));

        return new Vector2(x, y);
    }

}
