using System.Linq;
using System.Collections.Generic;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Server.Parallax;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.Procedural;

public sealed partial class DungeonGenerationSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSys = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly TileSystem _tileSys = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private readonly ProtoId<BiomeTemplatePrototype> _biomeProtoId = "Empty";
    private readonly ProtoId<ContentTileDefinition> _floorProtoId = "MedievalFloorStone9";
    private readonly EntProtoId _wallProtoId = "MedievalStoneBrickWallIndestructable";

    public override void Initialize()
    {
        base.Initialize();
        _console.RegisterCommand("impdungen", "Generate dungeon instantly", "impdungen", GenerateCommand);
    }

    private void GenerateCommand(IConsoleShell shell, string argstr, string[] args)
    {
        var random = new Random();
        var mapId = _mapSys.CreateMap();
        var grid = _mapManager.CreateGridEntity(mapId);

        if (_proto.TryIndex(_biomeProtoId, out var biomeProto))
        {
            _biome.EnsurePlanet(grid.Owner, biomeProto);
        }

        var width = 10;
        var height = 10;
        var roomSize = new Vector2i(10, 10); // Итоговый размер 100x100 тайлов

        var tileDef = (ContentTileDefinition)_tileDefMan[_floorProtoId];

        var tilesToSet = new List<(Vector2i, Tile)>();

        // Границы: от -100 до 0
        var minX = -(width * roomSize.X);
        var minY = -(height * roomSize.Y);
        var box = new Box2i(minX, minY, 0, 0);

        for (var x = box.Left; x < box.Right; x++)
        {
            for (var y = box.Bottom; y < box.Top; y++)
            {
                // Тут мы просто генерируем Variant, это быстро
                var tile = _tileSys.GetVariantTile(tileDef, random);
                tilesToSet.Add((new Vector2i(x, y), tile));
            }
        }

        if (TryComp<MapGridComponent>(grid.Owner, out var gridComp))
        {
            _mapSys.SetTiles(grid.Owner, gridComp, tilesToSet);
        }

        for (var x = box.Left; x <= box.Right; x++)
        {
            SpawnWall(grid.Owner, x, box.Bottom);
            if (box.Top != box.Bottom) SpawnWall(grid.Owner, x, box.Top);
        }

        for (var y = box.Bottom + 1; y < box.Top; y++)
        {
            SpawnWall(grid.Owner, box.Left, y);
            if (box.Right != box.Left) SpawnWall(grid.Owner, box.Right, y);
        }

        shell.WriteLine($"Dungeon generated on Map {mapId} (Grid {grid.Owner})!");
    }

    private void SpawnWall(EntityUid gridUid, int x, int y)
    {
        var coords = new EntityCoordinates(gridUid, new Vector2i(x, y));
        Spawn(_wallProtoId, coords);
    }
}
