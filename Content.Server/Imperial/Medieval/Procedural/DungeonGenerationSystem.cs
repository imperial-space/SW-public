using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Console;
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

    private readonly ProtoId<BiomeTemplatePrototype> _biomeProtoId = "Empty";
    private readonly ProtoId<ContentTileDefinition> _floorProtoId = "MedievalFloorStone9";
    private readonly EntProtoId _wallProtoId = "MedievalStoneBrickWallIndestructable";
    private readonly EntProtoId _doorProtoId = "MedievalAirlock";

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

    /// <summary>
    /// Основная точка входа. Рассчитывает размеры грида, создает карту и запускает этапы генерации.
    /// </summary>
    public bool GenerateDungeon(int width, int height, [NotNullWhen(true)] out Entity<MapGridComponent>? dungeonGrid)
    {
        dungeonGrid = null;
        var random = new Random();
        var tileDef = (ContentTileDefinition)_tileDefMan[_floorProtoId];

        var totalPixelWidth = width * (_size.X - 1) + 1;
        var totalPixelHeight = height * (_size.Y - 1) + 1;

        var minX = -totalPixelWidth / 2;
        var minY = -totalPixelHeight / 2;
        var maxX = minX + totalPixelWidth;
        var maxY = minY + totalPixelHeight;
        var dungeonBox = new Box2i(minX, minY, maxX, maxY);

        if (!PrepareDungeonBase(dungeonBox, tileDef, random, out dungeonGrid, out _))
            return false;

        var gridUid = dungeonGrid.Value.Owner;
        var gridComp = dungeonGrid.Value.Comp;

        var layout = GenerateLogicalLayout(width, height, random);
        BuildInternalStructure(gridUid, gridComp, layout, dungeonBox.BottomLeft, width, height);

        return true;
    }

    /// <summary>
    /// Создает карту, устанавливает биом, заливает тайлы пола и строит внешние стены по периметру.
    /// </summary>
    private bool PrepareDungeonBase(Box2i box, ContentTileDefinition tileDef, Random random, [NotNullWhen(true)] out Entity<MapGridComponent>? dungeonGrid, out MapId mapId)
    {
        dungeonGrid = null;
        var mapEntity = _mapSys.CreateMap(out mapId);

        if (_proto.TryIndex(_biomeProtoId, out var biomeProto))
            _biome.EnsurePlanet(mapEntity, biomeProto);

        if (!TryComp<MapGridComponent>(mapEntity, out var gridComp))
        {
            mapId = MapId.Nullspace;
            return false;
        }

        dungeonGrid = (mapEntity, gridComp);
        var tilesToSet = new List<(Vector2i, Tile)>();

        for (var x = box.Left; x < box.Right; x++)
        {
            for (var y = box.Bottom; y < box.Top; y++)
            {
                var tile = _tileSys.GetVariantTile(tileDef, random);
                tilesToSet.Add((new Vector2i(x, y), tile));
            }
        }
        _mapSys.SetTiles(mapEntity, gridComp, tilesToSet);

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

    /// <summary>
    /// Размещает физические стены и двери внутри данжа на основе логической схемы.
    /// </summary>
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
                    // Строгая проверка (wy == doorPos) гарантирует отсутствие дыр вокруг двери.
                    if (isConnected && wy == doorPos)
                        Spawn(_doorProtoId, _mapSys.ToCoordinates(gridUid, pos, grid));
                    else
                        SpawnWall(gridUid, grid, pos);
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
                    if (isConnected && wx == doorPos)
                        Spawn(_doorProtoId, _mapSys.ToCoordinates(gridUid, pos, grid));
                    else
                        SpawnWall(gridUid, grid, pos);
                }
            }
        }
    }

    private void SpawnWall(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        if (_mapSys.GetAnchoredEntities(gridUid, grid, pos).Any()) return;
        Spawn(_wallProtoId, _mapSys.ToCoordinates(gridUid, pos, grid));
    }

    /// <summary>
    /// Генерирует абстрактную схему дверей: создает остовное дерево, добавляет циклы и ищет альтернативные пути.
    /// </summary>
    private DungeonLayout GenerateLogicalLayout(int width, int height, Random random)
    {
        var layout = new DungeonLayout
        {
            HorizontalDoors = new bool[width - 1, height],
            VerticalDoors = new bool[width, height - 1],
            SpecialPairs = new List<(Vector2i Room, Vector2i DoorPos)>()
        };

        GenerateMaze(width, height, random, ref layout);
        AddLoops(width, height, random, ref layout);
        FindSpecialPairs(width, height, random, ref layout);

        return layout;
    }

    /// <summary>
    /// Алгоритм Recursive Backtracker для создания идеального лабиринта (гарантия связности).
    /// </summary>
    private void GenerateMaze(int width, int height, Random random, ref DungeonLayout layout)
    {
        var visited = new bool[width, height];
        var stack = new Stack<Vector2i>();

        var startX = 0;
        var startY = 0;
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

    /// <summary>
    /// Добавляет случайные проходы в стенах, соблюдая ограничение максимум 3 двери на комнату.
    /// </summary>
    private void AddLoops(int width, int height, Random random, ref DungeonLayout layout)
    {
        var potentialWalls = new List<(Vector2i CellA, Vector2i CellB, bool IsHorizontal)>();

        for (var x = 0; x < width - 1; x++)
            for (var y = 0; y < height; y++)
                if (!layout.HorizontalDoors[x, y])
                    potentialWalls.Add((new Vector2i(x, y), new Vector2i(x + 1, y), true));

        for (var x = 0; x < width; x++)
            for (var y = 0; y < height - 1; y++)
                if (!layout.VerticalDoors[x, y])
                    potentialWalls.Add((new Vector2i(x, y), new Vector2i(x, y + 1), false));

        var walls = potentialWalls.OrderBy(_ => random.Next()).ToList();

        foreach (var (a, b, isHor) in walls)
        {
            int doorsA = CountDoors(a, width, height, layout);
            int doorsB = CountDoors(b, width, height, layout);

            // Если у комнат уже 3 двери - пропускаем. Это также обеспечивает приоритет 1 > 2 > 3.
            if (doorsA >= 3 || doorsB >= 3) continue;

            if (random.NextDouble() < 0.20)
            {
                if (isHor)
                    layout.HorizontalDoors[Math.Min(a.X, b.X), a.Y] = true;
                else
                    layout.VerticalDoors[a.X, Math.Min(a.Y, b.Y)] = true;
            }
        }
    }

    private int CountDoors(Vector2i cell, int w, int h, DungeonLayout layout)
    {
        int count = 0;
        if (cell.X > 0 && layout.HorizontalDoors[cell.X - 1, cell.Y]) count++;
        if (cell.X < w - 1 && layout.HorizontalDoors[cell.X, cell.Y]) count++;
        if (cell.Y > 0 && layout.VerticalDoors[cell.X, cell.Y - 1]) count++;
        if (cell.Y < h - 1 && layout.VerticalDoors[cell.X, cell.Y]) count++;
        return count;
    }

    /// <summary>
    /// Находит пары (Комната, Дверь), где в комнату можно попасть от старта, не используя эту конкретную дверь.
    /// </summary>
    private void FindSpecialPairs(int width, int height, Random random, ref DungeonLayout layout)
    {
        var targetCount = (width * height) / 5;
        if (targetCount < 1) targetCount = 1;

        var allDoors = new List<(Vector2i CellA, Vector2i CellB, Vector2i DoorGridIndex, bool IsHor)>();

        for (var x = 0; x < width - 1; x++)
            for (var y = 0; y < height; y++)
                if (layout.HorizontalDoors[x, y])
                    allDoors.Add((new Vector2i(x, y), new Vector2i(x + 1, y), new Vector2i(x, y), true));

        for (var x = 0; x < width; x++)
            for (var y = 0; y < height - 1; y++)
                if (layout.VerticalDoors[x, y])
                    allDoors.Add((new Vector2i(x, y), new Vector2i(x, y + 1), new Vector2i(x, y), false));

        var shuffledDoors = allDoors.OrderBy(_ => random.Next()).ToList();
        var startNode = new Vector2i(0, 0);

        foreach (var (a, b, doorIdx, isHor) in shuffledDoors)
        {
            if (layout.SpecialPairs.Count >= targetCount) break;

            // Проверяем возможность пути в B, заблокировав текущую дверь
            if (b != startNode)
            {
                if (HasPath(startNode, b, doorIdx, isHor, width, height, layout))
                {
                    layout.SpecialPairs.Add((b, doorIdx));
                    continue;
                }
            }

            // Проверяем возможность пути в A, заблокировав текущую дверь
            if (a != startNode)
            {
                if (HasPath(startNode, a, doorIdx, isHor, width, height, layout))
                {
                    layout.SpecialPairs.Add((a, doorIdx));
                    continue;
                }
            }
        }
    }

    /// <summary>
    /// BFS поиск пути с учетом "виртуально закрытой" двери.
    /// </summary>
    private bool HasPath(Vector2i start, Vector2i end, Vector2i blockedDoorIdx, bool blockedDoorIsHor, int w, int h, DungeonLayout layout)
    {
        var q = new Queue<Vector2i>();
        var visited = new HashSet<Vector2i>();

        q.Enqueue(start);
        visited.Add(start);

        while (q.Count > 0)
        {
            var curr = q.Dequeue();
            if (curr == end) return true;

            // Вверх (y+1)
            if (curr.Y < h - 1 && layout.VerticalDoors[curr.X, curr.Y])
            {
                if (blockedDoorIsHor || blockedDoorIdx.X != curr.X || blockedDoorIdx.Y != curr.Y)
                {
                    var next = new Vector2i(curr.X, curr.Y + 1);
                    if (visited.Add(next)) q.Enqueue(next);
                }
            }
            // Вниз (y-1)
            if (curr.Y > 0 && layout.VerticalDoors[curr.X, curr.Y - 1])
            {
                if (blockedDoorIsHor || blockedDoorIdx.X != curr.X || blockedDoorIdx.Y != curr.Y - 1)
                {
                    var next = new Vector2i(curr.X, curr.Y - 1);
                    if (visited.Add(next)) q.Enqueue(next);
                }
            }
            // Вправо (x+1)
            if (curr.X < w - 1 && layout.HorizontalDoors[curr.X, curr.Y])
            {
                if (!blockedDoorIsHor || blockedDoorIdx.X != curr.X || blockedDoorIdx.Y != curr.Y)
                {
                    var next = new Vector2i(curr.X + 1, curr.Y);
                    if (visited.Add(next)) q.Enqueue(next);
                }
            }
            // Влево (x-1)
            if (curr.X > 0 && layout.HorizontalDoors[curr.X - 1, curr.Y])
            {
                if (!blockedDoorIsHor || blockedDoorIdx.X != curr.X - 1 || blockedDoorIdx.Y != curr.Y)
                {
                    var next = new Vector2i(curr.X - 1, curr.Y);
                    if (visited.Add(next)) q.Enqueue(next);
                }
            }
        }
        return false;
    }
}

public struct DungeonLayout
{
    public bool[,] HorizontalDoors;
    public bool[,] VerticalDoors;
    public List<(Vector2i Room, Vector2i DoorPos)> SpecialPairs;
}
