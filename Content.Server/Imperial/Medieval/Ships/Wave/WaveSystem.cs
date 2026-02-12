using System.Linq;
using System.Numerics;
using Content.Server.Destructible;
using Content.Shared.Construction.Conditions;
using Content.Shared.Damage;
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


    private readonly Random _random = new();
    public (string, ushort)[] Stages =
    {
        ("FloorWood", (ushort)1),
        ("FloorSteel", (ushort)2),
        ("Plating", (ushort)3),
        ("FloorWhite", (ushort)4)
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveComponent, StartCollideEvent>(OnCollide);

        foreach (var stage in Stages)
        {
            _tileDefinitionManager.TryGetDefinition(stage.Item1, out var tileDefinition);
            if (tileDefinition == null)
                continue;
            var full = Stages.Index();

            int index = 0;
            foreach (var hah in full)
            {
                if (hah.Item2 == stage)
                {
                    index = hah.Item1;
                    break;
                }
            }
            Stages[index] = (stage.Item1, tileDefinition.TileId);
        }
    }


    private void OnCollide(EntityUid uid, WaveComponent component, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OtherEntity))
            return;
        if (component.HitList.Contains(args.OtherEntity))
            return;

        var collisionPos = _transform.GetMapCoordinates(args.OurEntity);
        var gridEntity = args.OtherEntity;
        _popup.PopupEntity(Loc.GetString($"{gridEntity}"), uid);
        if (!_entityManager.TryGetComponent<MapGridComponent>(gridEntity, out var mapGridComp))
            return;
        _popup.PopupEntity(Loc.GetString($"{gridEntity}"), uid);

        var grid = new Entity<MapGridComponent>(gridEntity, mapGridComp);
        var tileRef = _map.GetTileRef(grid, collisionPos);

        var centerTilePos = tileRef.GridIndices;
        const float radiusTiles = 1.5f;


        var nearbyTiles = new List<Vector2i>();
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                var tilePos = centerTilePos + new Vector2i(dx, dy);
                var tile = _map.GetTileRef(grid, tilePos);
                if (tile.Tile.IsEmpty)
                    continue;

                var distance = Vector2.Distance(centerTilePos, tilePos);
                if (distance <= radiusTiles)
                    nearbyTiles.Add(tilePos);
            }
        }

        _random.Shuffle(nearbyTiles);
        int tilesToReplace = Math.Min(2, nearbyTiles.Count);



        for (int i = 0; i < tilesToReplace; i++)
        {
            var tilePos = nearbyTiles[i];
            _map.TryGetTile(grid, tilePos, out var tile);

            if (tile.TypeId == Stages[Stages.Length].Item2)
                continue;
            var index = 0;
            foreach (var stage in Stages)
            {
                if (stage.Item2 == tile.TypeId)
                    break;
                index++;
            }
            _map.SetTile(grid.Owner, grid , tilePos, new Tile(Stages[index+1].Item2, 0, 0));
        }
        if (!TerminatingOrDeleted(args.OtherEntity))
            component.HitList.Add(args.OtherEntity);
        _entityManager.DeleteEntity(uid);
    }
}
