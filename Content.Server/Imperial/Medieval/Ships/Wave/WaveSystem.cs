using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Shared.Construction.Conditions;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Tiles;
using Content.Shared.Trigger.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Wave;

public sealed class WaveSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly FloorTileSystem _floorTileSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminlogs = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly TagSystem _tags = default!;


    private readonly Random _random = new();
    private bool _initialized;

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveComponent, StartCollideEvent>(OnCollide);

    }

    private void OnCollide(EntityUid uid, WaveComponent component, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OtherEntity))
            return;
        if (component.HitList.Contains(args.OtherEntity))
            return;
        if (_cfg.GetCVar(ShipsCCVars.WaveMinToBreakLevel) > _cfg.GetCVar(ShipsCCVars.StormLevel))
            _entityManager.DeleteEntity(args.OurEntity);
        EnsureComp<TransformComponent>(args.OurEntity);
        var collisionPos = _transform.GetMapCoordinates(args.OurEntity);
        var gridEntity = args.OtherEntity;
        if (!_entityManager.TryGetComponent<MapGridComponent>(gridEntity, out var mapGridComp))
            return;

        var grid = new Entity<MapGridComponent>(gridEntity, mapGridComp);

        var tileRef = _map.GetTileRef(grid, collisionPos);

        var centerTilePos = _map.MapToGrid(grid, collisionPos);

        var radiusTiles = _cfg.GetCVar(ShipsCCVars.WaveRadiusTiles) + _cfg.GetCVar(ShipsCCVars.StormLevel);

        var antiradius = (int)radiusTiles*-1;

        var nearbyTiles = new List<Vector2i>();


        for (int dx = antiradius; dx <= radiusTiles; dx++)
        {
            for (int dy = antiradius; dy <= radiusTiles; dy++)
            {
                var tilePos = centerTilePos + new EntityCoordinates(gridEntity, new Vector2(dx, dy)) ;
                var tile = _map.GetTileRef(grid, tilePos);

                if (tile.Tile.IsEmpty)
                    continue;
                var wallcheck = new HashSet<EntityUid>();
                _lookup.GetEntitiesInTile(tile, wallcheck);
                var stop = false;
                foreach (var wall in wallcheck)
                {
                    if (_tags.HasTag(wall, "Wall"))
                    {
                        stop = true;
                    }

                }
                if (stop)
                    continue;

                var distance = Vector2.Distance(centerTilePos.Position, tilePos.Position);
                if (distance <= radiusTiles)
                    nearbyTiles.Add(((int)tilePos.X, (int)tilePos.Y));
            }
        }

        _random.Shuffle(nearbyTiles);

        int tilesToReplace = Math.Min(_random.Next(0,_cfg.GetCVar(ShipsCCVars.WaveMaxBreakCount)), nearbyTiles.Count);
        for (int i = 0; i < tilesToReplace; i++)
        {
            var tilePos = nearbyTiles[i];
            if (!_map.TryGetTile(grid, tilePos, out var tile))
                continue;
            _tileDefinitionManager.TryGetDefinition("FloorBrokenWoodDDD", out var floorDef);
            if (floorDef == null)
            {
                Log.Error("Пол не найден");
                return;
            }
            var stagelast = floorDef.Variants;

            if (tile.TypeId != floorDef.TileId)
            {
                _map.SetTile(grid.Owner, grid , tilePos, new Tile(floorDef.TileId, 0, 0));
                continue;
            }

            if (tile.TypeId == stagelast || tile.IsEmpty)
                continue;

            var variant = (byte)(tile.Variant + 1);
            _map.SetTile(grid.Owner, grid , tilePos, new Tile(floorDef.TileId, 0, variant));
        }
        if (!TerminatingOrDeleted(args.OtherEntity))
            component.HitList.Add(args.OtherEntity);
        if (component.DeleteOnCollide)
            _entityManager.DeleteEntity(args.OurEntity);
    }
    /// <summary>
    /// призывает грид на куазанных координатах относительно какой то сущности и даёт ей силу
    /// coords кординаты это сущность от которой считать и вектор смещения
    /// mapId айди мапы
    /// force вектор силы который мы прикладываем если надо
    /// deleteOnCollide при столкновении удаляем
    /// lifetime = 0 не будет удалять сущность по истечению таймера
    /// </summary>
    public void SpawnWave(EntityCoordinates coords, MapId mapId, Vector2 force = new Vector2(), bool deleteOnCollide = true, float lifetime = 60)
    {
        if (!_map.TryGetMap(mapId, out var mapEntity))
            return;

        var grid = _mapManager.CreateGridEntity(mapId);
        _transform.SetParent(grid, mapEntity.Value);
        var waveComponent = EnsureComp<WaveComponent>(grid);
        waveComponent.DeleteOnCollide = deleteOnCollide;
        _tileDefinitionManager.TryGetDefinition("FloorWood", out var tileDefinition);// сюда поставить воду
        if (tileDefinition == null)
            return;
        _map.SetTile(grid, new Vector2i(0,0), new Tile(tileDefinition.TileId, 0, 0));// создаёт тайлик воды надо поставить воду вон туда
        if (HasComp<TransformComponent>(grid))
        {
            _transform.SetCoordinates(grid, coords);
            _physics.WakeBody(grid);
            _physics.ApplyLinearImpulse(grid, force);
            if (lifetime > 0)
            {
                var despawnComponent = EnsureComp<MedievalTimedDespawnComponent>(grid);
                despawnComponent.Lifetime = lifetime;
                despawnComponent.OriginalLifeTime = lifetime;
            }
        }
    }
}
