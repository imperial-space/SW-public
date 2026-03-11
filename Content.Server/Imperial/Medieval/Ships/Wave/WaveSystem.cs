using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Shared.Construction.Conditions;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tiles;
using Content.Shared.Trigger.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
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


    private readonly Random _random = new();
    public (string, ushort)[] Stages =
    {
        ("FloorWood", (ushort)1),
        ("FloorSteel", (ushort)2),
        ("Plating", (ushort)3),
        ("FloorWhite", (ushort)4)
    };
    private const float RadiusTiles = 3f;
    private bool _initialized;

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveComponent, StartCollideEvent>(OnCollide);

    }
    private void Startup()
    {
        if (_initialized)
            return;
        if (Stages == null || Stages.Length == 0)
            return;

        for (int i = 0; i < Stages.Length; i++)
        {
            var stage = Stages[i];
            if (!_tileDefinitionManager.TryGetDefinition(stage.Item1, out var tileDefinition))
                continue;

            Stages[i] = (stage.Item1, tileDefinition.TileId);
        }
        _initialized = true;
    }

    private void OnCollide(EntityUid uid, WaveComponent component, ref StartCollideEvent args)
    {
        if (!_initialized)
            Startup();
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OtherEntity))
            return;
        if (component.HitList.Contains(args.OtherEntity))
            return;
        EnsureComp<TransformComponent>(args.OurEntity);
        var collisionPos = _transform.GetMapCoordinates(args.OurEntity);
        var gridEntity = args.OtherEntity;
        if (!_entityManager.TryGetComponent<MapGridComponent>(gridEntity, out var mapGridComp))
            return;

        var grid = new Entity<MapGridComponent>(gridEntity, mapGridComp);

        var tileRef = _map.GetTileRef(grid, collisionPos);

        var centerTilePos = _map.MapToGrid(grid, collisionPos);

        var antiradius = (int)RadiusTiles*-1;

        var nearbyTiles = new List<Vector2i>();

        for (int dx = antiradius; dx <= RadiusTiles; dx++)
        {
            for (int dy = antiradius; dy <= RadiusTiles; dy++)
            {
                var tilePos = centerTilePos + new EntityCoordinates(gridEntity, new Vector2(dx, dy)) ;
                var tile = _map.GetTileRef(grid, tilePos);

                if (tile.Tile.IsEmpty)
                    continue;

                var distance = Vector2.Distance(centerTilePos.Position, tilePos.Position);
                if (distance <= RadiusTiles)
                    nearbyTiles.Add(((int)tilePos.X, (int)tilePos.Y));
            }
        }

        _random.Shuffle(nearbyTiles);

        int tilesToReplace = Math.Min(_random.Next(0,4), nearbyTiles.Count);
        _popup.PopupEntity(Loc.GetString($"   {tilesToReplace}"), uid);
        for (int i = 0; i < tilesToReplace; i++)
        {
            var tilePos = nearbyTiles[i];
            if (!_map.TryGetTile(grid, tilePos, out var tile))
                continue;
            var stagelast = Stages.Length-1;

            if (tile.TypeId == Stages[stagelast].Item2 || tile.IsEmpty)
                continue;
            var index = 0;
            foreach (var stage in Stages)
            {
                if (stage.Item2 == tile.TypeId)
                    break;
                index++;
            }
            if (index == stagelast+1)
                index = 0;
            _map.SetTile(grid.Owner, grid , tilePos, new Tile(Stages[index+1].Item2, 0, 0));
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
        var grid = _mapManager.CreateGridEntity(mapId);
        var waveComponent = EnsureComp<WaveComponent>(grid);
        waveComponent.DeleteOnCollide = deleteOnCollide;
        _tileDefinitionManager.TryGetDefinition("FloorWood", out var tileDefinition);// сюда поставить воду
        if (tileDefinition == null)
            return;
        _map.SetTile(grid, new Vector2i(0,0),new Tile(tileDefinition.TileId, 0, 0));// создаёт тайлик воды надо поставить воду вон туда
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
