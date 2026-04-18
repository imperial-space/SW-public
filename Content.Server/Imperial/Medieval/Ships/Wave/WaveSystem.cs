using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Hull;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Content.Shared.Tiles;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Medieval.Ships.Wave;

public sealed class WaveSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedShipHullSystem _shipHull = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<Vector2i> _nearbyTiles = new();
    private readonly HashSet<EntityUid> _tileContents = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(EntityUid uid, WaveComponent component, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OtherEntity))
            return;

        var targetEntity = args.OtherEntity;
        MapGridComponent? mapGridComp = null;

        if (!TryComp<MapGridComponent>(targetEntity, out mapGridComp))
        {
            var gridUid = _transform.GetGrid(targetEntity);
            if (!gridUid.HasValue || !TryComp<MapGridComponent>(gridUid.Value, out mapGridComp))
            {
                if (component.DeleteOnCollide)
                    EntityManager.DeleteEntity(args.OurEntity);

                return;
            }

            targetEntity = gridUid.Value;
        }

        if (component.HitList.Contains(targetEntity))
            return;

        if (!HasComp<ShipDrowningComponent>(targetEntity))
        {
            if (component.DeleteOnCollide)
                EntityManager.DeleteEntity(args.OurEntity);

            return;
        }

        if (_cfg.GetCVar(ShipsCCVars.WaveMinToBreakLevel) > _cfg.GetCVar(ShipsCCVars.StormLevel))
        {
            if (component.DeleteOnCollide)
                EntityManager.DeleteEntity(args.OurEntity);

            return;
        }

        var grid = new Entity<MapGridComponent>(targetEntity, mapGridComp);
        var collisionPos = _transform.GetMapCoordinates(args.OurEntity);
        var centerTilePos = _map.MapToGrid(grid, collisionPos);
        var radiusTiles = _cfg.GetCVar(ShipsCCVars.WaveRadiusTiles) + _cfg.GetCVar(ShipsCCVars.StormLevel);
        var radiusLimit = (int) MathF.Ceiling(radiusTiles);
        var radiusSquared = radiusTiles * radiusTiles;

        _nearbyTiles.Clear();

        for (var dx = -radiusLimit; dx <= radiusLimit; dx++)
        {
            for (var dy = -radiusLimit; dy <= radiusLimit; dy++)
            {
                var tileCoordinates = centerTilePos.Offset(new Vector2(dx, dy));
                if (!_map.TryGetTileRef(grid.Owner, grid.Comp, tileCoordinates, out var tile))
                    continue;

                if (tile.Tile.IsEmpty || !_shipHull.TryGetDamageStage(tile.Tile.TypeId, out _))
                    continue;

                _tileContents.Clear();
                _lookup.GetEntitiesInTile(tile, _tileContents);

                var blockedByWall = false;
                foreach (var tileEntity in _tileContents)
                {
                    if (!_tags.HasTag(tileEntity, "Wall"))
                        continue;

                    blockedByWall = true;
                    break;
                }

                if (blockedByWall)
                    continue;

                var delta = tileCoordinates.Position - centerTilePos.Position;
                if (delta.LengthSquared() <= radiusSquared)
                    _nearbyTiles.Add(tile.GridIndices);
            }
        }

        if (_nearbyTiles.Count == 0)
        {
            if (component.DeleteOnCollide)
                EntityManager.DeleteEntity(args.OurEntity);

            return;
        }

        var maxBreakCount = Math.Max(0, _cfg.GetCVar(ShipsCCVars.WaveMaxBreakCount));
        if (maxBreakCount > 0)
        {
            _random.Shuffle(_nearbyTiles);

            var tilesToReplace = Math.Min(_random.Next(1, maxBreakCount + 1), _nearbyTiles.Count);
            for (var i = 0; i < tilesToReplace; i++)
            {
                var tilePos = _nearbyTiles[i];
                if (!_map.TryGetTileRef(grid.Owner, grid.Comp, tilePos, out var tile) || tile.Tile.IsEmpty)
                    continue;

                if (!_shipHull.TryGetNextDamageTile(tile.Tile.TypeId, out var damagedTileType))
                    continue;

                _map.SetTile(grid.Owner, grid, tilePos, new Tile(damagedTileType, 0, 0));
            }
        }

        if (!TerminatingOrDeleted(targetEntity))
            component.HitList.Add(targetEntity);

        if (component.DeleteOnCollide)
            EntityManager.DeleteEntity(args.OurEntity);
    }

    public EntityUid? SpawnWave(MapCoordinates coords, Vector2 force = default, bool deleteOnCollide = true, float lifetime = 60)
    {
        if (!_map.TryGetMap(coords.MapId, out _))
            return null;

        var wave = Spawn("WaveLarge", coords);
        var waveComponent = EnsureComp<WaveComponent>(wave);
        waveComponent.DeleteOnCollide = deleteOnCollide;

        _physics.WakeBody(wave);
        _physics.ApplyLinearImpulse(wave, force);

        RemComp<TimedDespawnComponent>(wave);
        RemComp<MedievalTimedDespawnComponent>(wave);
        if (lifetime <= 0)
            return wave;

        var despawnComponent = EnsureComp<MedievalTimedDespawnComponent>(wave);
        despawnComponent.Lifetime = lifetime;
        despawnComponent.OriginalLifeTime = lifetime;
        return wave;
    }
}
