using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Console;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Server.Parallax;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Procedural;

public sealed partial class DungeonGenerationSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSys = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly TileSystem _tileSys = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // Константы и прототипы тайлов/стен
    private readonly ProtoId<BiomeTemplatePrototype> _biomeProtoId = "Empty";
    private readonly ProtoId<ContentTileDefinition> _floorProtoId = "MedievalFloorStone9";
    private readonly EntProtoId _wallProtoId = "MedievalStoneBrickWallIndestructable";
    private readonly EntProtoId _doorProtoId = "MedievalAirlock";

    // Размер одной ячейки (комнаты)
    private readonly Vector2i _size = new Vector2i(15, 15);

    public override void Initialize()
    {
        base.Initialize();
        _console.RegisterCommand("impdungen", "Generate dungeon instantly", "impdungen", GenerateCommand);
    }

    private void GenerateCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (!GenerateDungeon(5, 5, out var grid))
        {
            shell.WriteLine("Failed to generate dungeon.");
        }
        else
        {
            shell.WriteLine($"Created bio-dungeon on Map {grid.Value.Owner}");
        }
    }

    public bool GenerateDungeon(int width, int height, [NotNullWhen(true)] out Entity<MapGridComponent>? dungeonGrid)
    {
        dungeonGrid = null;
        var random = new Random();

        var tileDef = (ContentTileDefinition)_tileDefMan[_floorProtoId];

        // Расчет размеров: (Кол-во * (Размер - 1)) + 1.
        // -1 нужно для наложения стен соседних комнат друг на друга.
        var totalPixelWidth = width * (_size.X - 1) + 1;
        var totalPixelHeight = height * (_size.Y - 1) + 1;

        var minX = -totalPixelWidth / 2;
        var minY = -totalPixelHeight / 2;
        var maxX = minX + totalPixelWidth;
        var maxY = minY + totalPixelHeight;

        var dungeonBox = new Box2i(minX, minY, maxX, maxY);

        // 1. Создаем карту и базовый грид (пол + периметр)
        if (!PrepareDungeonBase(dungeonBox, tileDef, random, out dungeonGrid, out var mapId))
        {
            return false;
        }

        var gridUid = dungeonGrid.Value.Owner;
        var gridComp = dungeonGrid.Value.Comp;

        // 2. Генерируем логику лабиринта (только стены и двери)
        var layout = GenerateLogicalLayout(width, height, random);

        // 3. Строим внутренние стены и двери (физическое разделение)
        BuildInternalStructure(gridUid, gridComp, layout, dungeonBox.BottomLeft, width, height);

        return true;
    }

    private bool PrepareDungeonBase(
        Box2i box,
        ContentTileDefinition tileDef,
        Random random,
        [NotNullWhen(true)] out Entity<MapGridComponent>? dungeonGrid,
        out MapId mapId)
    {
        dungeonGrid = null;
        var mapEntity = _mapSys.CreateMap(out mapId);

        if (_proto.TryIndex(_biomeProtoId, out var biomeProto))
        {
            _biome.EnsurePlanet(mapEntity, biomeProto);
        }

        if (!TryComp<MapGridComponent>(mapEntity, out var gridComp))
        {
            mapId = MapId.Nullspace;
            return false;
        }

        dungeonGrid = (mapEntity, gridComp);

        var tilesToSet = new List<(Vector2i, Tile)>();

        // Заполняем пол
        for (var x = box.Left; x < box.Right; x++)
        {
            for (var y = box.Bottom; y < box.Top; y++)
            {
                var tile = _tileSys.GetVariantTile(tileDef, random);
                tilesToSet.Add((new Vector2i(x, y), tile));
            }
        }
        _mapSys.SetTiles(mapEntity, gridComp, tilesToSet);

        // Строим стены по периметру
        var minX = box.Left;
        var minY = box.Bottom;
        var maxX = box.Right - 1;
        var maxY = box.Top - 1;

        for (var x = minX; x <= maxX; x++)
        {
            SpawnWall(mapEntity, gridComp, new Vector2i(x, minY));
            SpawnWall(mapEntity, gridComp, new Vector2i(x, maxY));
        }
        for (var y = minY; y <= maxY; y++)
        {
            SpawnWall(mapEntity, gridComp, new Vector2i(minX, y));
            SpawnWall(mapEntity, gridComp, new Vector2i(maxX, y));
        }

        return true;
    }

    private void BuildInternalStructure(EntityUid gridUid, MapGridComponent grid, DungeonLayout layout, Vector2i startPos, int width, int height)
    {
        var stepX = _size.X - 1;
        var stepY = _size.Y - 1;

        // Вертикальные перегородки
        for (var i = 1; i < width; i++)
        {
            var wallX = startPos.X + (i * stepX);
            for (var y = 0; y < height; y++)
            {
                var roomStartY = startPos.Y + (y * stepY);
                var roomEndY = roomStartY + stepY;
                var doorPos = roomStartY + (stepY / 2);

                bool isConnected = layout.HorizontalDoors[i - 1, y];

                for (var wy = roomStartY; wy <= roomEndY; wy++)
                {
                    var pos = new Vector2i(wallX, wy);

                    if (isConnected && wy == doorPos)
                    {
                        Spawn(_doorProtoId, _mapSys.ToCoordinates(gridUid, pos, grid));
                    }
                    else
                    {
                        SpawnWall(gridUid, grid, pos);
                    }
                }
            }
        }

        // Горизонтальные перегородки
        for (var j = 1; j < height; j++)
        {
            var wallY = startPos.Y + (j * stepY);
            for (var x = 0; x < width; x++)
            {
                var roomStartX = startPos.X + (x * stepX);
                var roomEndX = roomStartX + stepX;
                var doorPos = roomStartX + (stepX / 2);

                bool isConnected = layout.VerticalDoors[x, j - 1];

                for (var wx = roomStartX; wx <= roomEndX; wx++)
                {
                    var pos = new Vector2i(wx, wallY);

                    // ИСПРАВЛЕНО: Аналогично, строго позиция двери
                    if (isConnected && wx == doorPos)
                    {
                        Spawn(_doorProtoId, _mapSys.ToCoordinates(gridUid, pos, grid));
                    }
                    else
                    {
                        SpawnWall(gridUid, grid, pos);
                    }
                }
            }
        }
    }

    private void SpawnWall(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        // Проверяем, нет ли уже стены (чтобы на углах не было дубликатов)
        // Не самый эффективный метод GetAnchoredEntities для массовой генерации,
        // но при небольших размерах данжа приемлемо.
        if (_mapSys.GetAnchoredEntities(gridUid, grid, pos).Any())
            return;

        Spawn(_wallProtoId, _mapSys.ToCoordinates(gridUid, pos, grid));
    }

    private DungeonLayout GenerateLogicalLayout(int width, int height, Random random)
    {
        // Теперь здесь не выбираются прототипы комнат, только создается граф дверей
        var layout = new DungeonLayout
        {
            HorizontalDoors = new bool[width - 1, height],
            VerticalDoors = new bool[width, height - 1]
        };
        GenerateMaze(width, height, random, ref layout);
        return layout;
    }

    private void GenerateMaze(int width, int height, Random random, ref DungeonLayout layout)
    {
        var visited = new bool[width, height];
        var stack = new Stack<Vector2i>();
        var startX = random.Next(width);
        var startY = random.Next(height);
        stack.Push(new Vector2i(startX, startY));
        visited[startX, startY] = true;
        var directions = new Vector2i[] { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var unvisited = new List<Vector2i>();
            foreach (var dir in directions)
            {
                var n = current + dir;
                if (n.X >= 0 && n.X < width && n.Y >= 0 && n.Y < height && !visited[n.X, n.Y])
                    unvisited.Add(n);
            }

            if (unvisited.Count > 0)
            {
                var next = unvisited[random.Next(unvisited.Count)];
                if (next.X > current.X) layout.HorizontalDoors[current.X, current.Y] = true;
                else if (next.X < current.X) layout.HorizontalDoors[next.X, current.Y] = true;
                else if (next.Y > current.Y) layout.VerticalDoors[current.X, current.Y] = true;
                else if (next.Y < current.Y) layout.VerticalDoors[current.X, next.Y] = true;

                visited[next.X, next.Y] = true;
                stack.Push(next);
            }
            else stack.Pop();
        }
    }
}

// Структура упрощена: убрано поле Rooms[,]
public struct DungeonLayout
{
    public bool[,] HorizontalDoors;
    public bool[,] VerticalDoors;
}
