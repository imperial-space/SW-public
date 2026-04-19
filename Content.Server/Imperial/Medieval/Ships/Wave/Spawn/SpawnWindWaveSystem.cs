using System;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Wave.Spawn;

public sealed class SpawnWindWaveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly WaveSystem _wave = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private TimeSpan _nextCheckTime;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime <= _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WaveDelay));
        foreach (var seaComponent in EntityManager.EntityQuery<SeaComponent>())
        {
            if (seaComponent.Disabled)
                continue;
            var sea = seaComponent.Owner;
            var seaMapId = _transform.GetMapId(sea);
            var ships = new HashSet<Entity<ShipDrowningComponent>>();
            _lookup.GetEntitiesOnMap(seaMapId, ships);

            foreach (var shipComp in ships)
            {
                var ship = shipComp.Owner;
                if (!TryComp<MapGridComponent>(ship, out var grid))
                    continue;

                var maxWaves = Math.Max(0, (int) MathF.Ceiling(_cfg.GetCVar(ShipsCCVars.StormLevel)));
                var waveCount = _random.Next(0, maxWaves + 1);
                var shipCenter = _transform.ToMapCoordinates(new EntityCoordinates(ship, grid.LocalAABB.Center));
                var shipRadius = grid.LocalAABB.Size.Length() * 0.5f;

                for (var i = 0; i < waveCount; i++)
                {
                    var waveOffset = GenerateWave();
                    var offsetLength = waveOffset.Length();
                    if (offsetLength <= 0f)
                        continue;

                    var direction = waveOffset / offsetLength;
                    var spawnDistance = shipRadius + offsetLength;
                    var wavePosition = shipCenter.Position + direction * spawnDistance;
                    var waveCoords = new MapCoordinates(wavePosition, seaMapId);
                    var forceDirection = shipCenter.Position - wavePosition;
                    var force = forceDirection.Normalized() * _cfg.GetCVar(ShipsCCVars.WaveForce);
                    _wave.SpawnWave(waveCoords, force);
                }
            }
        }
    }

    private Vector2 GenerateWave(float radius = 0, float targetAngle = 0, float halfAngle = 3.0235f)
    {
        if (halfAngle == 3.0235f)
            halfAngle = _cfg.GetCVar(ShipsCCVars.WaveSpawnAngle) * _cfg.GetCVar(ShipsCCVars.StormLevel);

        if (targetAngle == 0)
            targetAngle = _cfg.GetCVar(ShipsCCVars.WindRotation);

        if (radius == 0)
            radius = _cfg.GetCVar(ShipsCCVars.WaveSpawnRange);

        var targetAngleRad = targetAngle * MathF.PI / 180f;
        var halfAngleRad = halfAngle * MathF.PI / 180f;
        var u = _random.NextDouble();
        var v = _random.NextDouble();
        var rho = radius * Math.Sqrt(u);

        var phiMin = targetAngleRad - halfAngleRad;
        var phiMax = targetAngleRad + halfAngleRad;
        var phi = phiMin + (phiMax - phiMin) * v;

        var x = (float) (rho * Math.Cos(phi));
        var y = (float) (rho * Math.Sin(phi));
        return new Vector2(x, y);
    }
}
