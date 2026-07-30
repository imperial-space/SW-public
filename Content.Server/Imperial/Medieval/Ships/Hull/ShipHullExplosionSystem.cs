using System.Collections.Generic;
using Content.Shared.Explosion;
using Content.Shared.Imperial.Medieval.Ships.Hull;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Ships.Hull;

public sealed class ShipHullExplosionSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedShipHullSystem _shipHull = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<(EntityUid Grid, Vector2i Indices), EntityUid> _markers = new();
    private readonly List<(EntityUid Grid, Vector2i Indices)> _markerRemoval = new();
    private readonly HashSet<EntityUid> _tileContents = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ShipDrowningComponent, ComponentStartup>(OnShipStartup);
        SubscribeLocalEvent<ShipDrowningComponent, ComponentShutdown>(OnShipShutdown);
        SubscribeLocalEvent<ShipDrowningComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ShipHullExplosionTargetComponent, ComponentShutdown>(OnMarkerShutdown);
        SubscribeLocalEvent<ShipHullExplosionTargetComponent, BeforeExplodeEvent>(OnBeforeExplode);
    }

    private void OnShipStartup(Entity<ShipDrowningComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<MapGridComponent>(entity, out var grid))
            return;

        var gridEntity = new Entity<MapGridComponent>(entity.Owner, grid);
        var tiles = _map.GetAllTilesEnumerator(gridEntity.Owner, gridEntity.Comp);
        while (tiles.MoveNext(out var tile))
        {
            if (_shipHull.TryGetDamageStage(tile.Value.Tile.TypeId, out _))
                EnsureMarker(gridEntity, tile.Value.GridIndices);
        }
    }

    private void OnShipShutdown(Entity<ShipDrowningComponent> entity, ref ComponentShutdown args)
    {
        _markerRemoval.Clear();
        foreach (var key in _markers.Keys)
        {
            if (key.Grid == entity.Owner)
                _markerRemoval.Add(key);
        }

        foreach (var key in _markerRemoval)
            RemoveMarker(key);
    }

    private void OnTileChanged(Entity<ShipDrowningComponent> entity, ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            var key = (entity.Owner, change.GridIndices);
            if (_shipHull.TryGetDamageStage(change.NewTile.TypeId, out _))
                EnsureMarker(args.Entity, change.GridIndices);
            else
                RemoveMarker(key);
        }
    }

    private void OnMarkerShutdown(Entity<ShipHullExplosionTargetComponent> entity, ref ComponentShutdown args)
    {
        var key = (entity.Comp.Grid, entity.Comp.GridIndices);
        if (_markers.GetValueOrDefault(key) == entity.Owner)
            _markers.Remove(key);
    }

    private void OnBeforeExplode(Entity<ShipHullExplosionTargetComponent> entity, ref BeforeExplodeEvent args)
    {
        var marker = entity.Comp;
        if (!TryComp<MapGridComponent>(marker.Grid, out var grid) ||
            !HasComp<ShipDrowningComponent>(marker.Grid) ||
            !_map.TryGetTileRef(marker.Grid, grid, marker.GridIndices, out var tile) ||
            !_shipHull.TryGetNextDamageTile(tile.Tile.TypeId, out var damagedTileType) ||
            _shipHull.IsBreakagePrevented(tile, _tileContents) ||
            !_prototypes.TryIndex<ExplosionPrototype>(args.Id, out var explosionType))
        {
            return;
        }

        var damagePerIntensity = explosionType.DamagePerIntensity.GetTotal().Float();
        if (damagePerIntensity <= 0f)
            return;

        var intensity = args.Damage.GetTotal().Float() / damagePerIntensity;
        if (!_random.Prob(explosionType.TileBreakChance(intensity)))
            return;

        var gridEntity = new Entity<MapGridComponent>(marker.Grid, grid);
        _map.SetTile(
            gridEntity.Owner,
            gridEntity,
            marker.GridIndices,
            _shipHull.WithTileType(tile.Tile, damagedTileType));
    }

    private void EnsureMarker(Entity<MapGridComponent> grid, Vector2i indices)
    {
        var key = (grid.Owner, indices);
        if (_markers.TryGetValue(key, out var existing) && Exists(existing))
            return;

        var marker = Spawn(null, _map.GridTileToLocal(grid.Owner, grid.Comp, indices));
        var markerComponent = AddComp<ShipHullExplosionTargetComponent>(marker);
        markerComponent.Grid = grid.Owner;
        markerComponent.GridIndices = indices;
        _markers[key] = marker;
    }

    private void RemoveMarker((EntityUid Grid, Vector2i Indices) key)
    {
        if (!_markers.Remove(key, out var marker) || !Exists(marker))
            return;

        QueueDel(marker);
    }
}
