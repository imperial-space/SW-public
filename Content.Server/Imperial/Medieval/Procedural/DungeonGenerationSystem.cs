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
using Robust.Shared.Random;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Storage.Components;

namespace Content.Server.Imperial.Medieval.Procedural;

public sealed partial class DungeonGenerationSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSys = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly TileSystem _tileSys = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    // --- Прототипы ---
    private readonly ProtoId<BiomeTemplatePrototype> _biomeProtoId = "Empty";
    private readonly ProtoId<ContentTileDefinition> _floorProtoId = "MedievalFloorStone9";
    private readonly EntProtoId _defaultDoorProtoId = "MedievalAirlock";
    private readonly EntProtoId _centerEntityProtoId = "MedievalDungeonRoomMarker";

    // Тег для поиска сундуков
    private readonly ProtoId<TagPrototype> _chestTag = "MedievalChest";

    private readonly EntProtoId[] _wallProtoIds =
    [
        "MedievalStoneBrickWallIndestructable",
        "WallReinforced", //here I need to replace with other 2 walls, which are not sprited now
        "WallReinforcedRust"
    ];

    private readonly Vector2i _size = new Vector2i(15, 15);

    public void GenerateDungeon(int width, int height, out MapId mapId, out List<EntityCoordinates> toSpawn)
    {
        toSpawn = new();
        var random = new Random();
        var layout = GenerateLogicalLayout(width, height, random);
        var totalPixelWidth = width * (_size.X - 1) + 1;
        var totalPixelHeight = height * (_size.Y - 1) + 1;
        var dungeonBox = new Box2i(-totalPixelWidth / 2, -totalPixelHeight / 2, -totalPixelWidth / 2 + totalPixelWidth, -totalPixelHeight / 2 + totalPixelHeight);

        if (!PrepareDungeonBase(dungeonBox, random, out var dungeonGrid, out mapId))
            return;

        var gridUid = dungeonGrid.Value.Owner;
        var gridComp = dungeonGrid.Value.Comp;

        BuildInternalStructure(gridUid, gridComp, layout, dungeonBox.BottomLeft, width, height);

        SpawnRoomDecoration(gridUid, gridComp, dungeonBox.BottomLeft, width, height);

        SpawnKeysInExistingChests(gridUid, gridComp, dungeonBox.BottomLeft, layout.SpecialPairs);

        GetPlayerSpawns(gridUid, gridComp, dungeonBox.BottomLeft, layout.StartRooms, out toSpawn);
    }

    private bool PrepareDungeonBase(Box2i box, Random random, [NotNullWhen(true)] out Entity<MapGridComponent>? dungeonGrid, out MapId mapId)
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
        var tileDef = (ContentTileDefinition)_tileDefMan[_floorProtoId];
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

        var minX = box.Left; var minY = box.Bottom;
        var maxX = box.Right - 1; var maxY = box.Top - 1;

        for (var x = minX; x <= maxX; x++) { SpawnWall(mapEntity, gridComp, new Vector2i(x, minY)); SpawnWall(mapEntity, gridComp, new Vector2i(x, maxY)); }
        for (var y = minY; y <= maxY; y++) { SpawnWall(mapEntity, gridComp, new Vector2i(minX, y)); SpawnWall(mapEntity, gridComp, new Vector2i(maxX, y)); }

        return true;
    }

    private void BuildInternalStructure(EntityUid gridUid, MapGridComponent grid, DungeonLayout layout, Vector2i startPos, int width, int height)
    {
        var stepX = _size.X - 1;
        var stepY = _size.Y - 1;

        var specialDoorLookup = new Dictionary<(Vector2i, bool), int>();
        for (int i = 0; i < layout.SpecialPairs.Count; i++)
        {
            var pair = layout.SpecialPairs[i];
            specialDoorLookup[(pair.DoorIdx, pair.IsHor)] = i;
        }

        for (var i = 1; i < width; i++)
        {
            var wallX = startPos.X + (i * stepX);
            for (var y = 0; y < height; y++)
            {
                var roomStartY = startPos.Y + (y * stepY);
                var doorPos = roomStartY + (stepY / 2);
                bool isConnected = layout.HorizontalDoors[i - 1, y];
                int? specialIndex = null;
                if (specialDoorLookup.TryGetValue((new Vector2i(i - 1, y), true), out var idx)) specialIndex = idx;

                for (var wy = roomStartY; wy <= roomStartY + stepY; wy++)
                {
                    var pos = new Vector2i(wallX, wy);
                    if (isConnected && wy == doorPos)
                    {
                        var proto = _defaultDoorProtoId;
                        if (specialIndex.HasValue) proto = $"MedievalAirlockDungeon{GetLetter(specialIndex.Value)}";
                        Spawn(proto, _mapSys.ToCoordinates(gridUid, pos, grid));
                    }
                    else SpawnWall(gridUid, grid, pos);
                }
            }
        }

        for (var j = 1; j < height; j++)
        {
            var wallY = startPos.Y + (j * stepY);
            for (var x = 0; x < width; x++)
            {
                var roomStartX = startPos.X + (x * stepX);
                var doorPos = roomStartX + (stepX / 2);
                bool isConnected = layout.VerticalDoors[x, j - 1];
                int? specialIndex = null;
                if (specialDoorLookup.TryGetValue((new Vector2i(x, j - 1), false), out var idx)) specialIndex = idx;

                for (var wx = roomStartX; wx <= roomStartX + stepX; wx++)
                {
                    var pos = new Vector2i(wx, wallY);
                    if (isConnected && wx == doorPos)
                    {
                        var proto = _defaultDoorProtoId;
                        if (specialIndex.HasValue) proto = $"MedievalAirlockDungeon{GetLetter(specialIndex.Value)}";
                        Spawn(proto, _mapSys.ToCoordinates(gridUid, pos, grid));
                    }
                    else SpawnWall(gridUid, grid, pos);
                }
            }
        }
    }

    private void SpawnRoomDecoration(EntityUid gridUid, MapGridComponent grid, Vector2i startPos, int width, int height)
    {
        var stepX = _size.X - 1;
        var stepY = _size.Y - 1;
        var halfX = _size.X / 2;
        var halfY = _size.Y / 2;

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var roomCenterX = startPos.X + (x * stepX) + halfX;
                var roomCenterY = startPos.Y + (y * stepY) + halfY;

                Spawn(_centerEntityProtoId, _mapSys.ToCoordinates(gridUid, new Vector2i(roomCenterX, roomCenterY), grid));
            }
        }
    }

    private void SpawnKeysInExistingChests(EntityUid gridUid, MapGridComponent grid, Vector2i startPos, List<(Vector2i Room, Vector2i DoorIdx, bool IsHor)> specialPairs)
    {
        var stepX = _size.X - 1;
        var stepY = _size.Y - 1;
        var halfX = _size.X / 2;
        var halfY = _size.Y / 2;

        for (int i = 0; i < specialPairs.Count; i++)
        {
            var pair = specialPairs[i];
            var letter = GetLetter(i);
            var keyProto = $"MedievalKeyDungeon{letter}";

            var roomIdx = pair.Room;
            var roomStartX = startPos.X + roomIdx.X * stepX + 1;
            var roomStartY = startPos.Y + roomIdx.Y * stepY + 1;
            var roomEndX = roomStartX + stepX - 2;
            var roomEndY = roomStartY + stepY - 2;

            EntityUid? targetChest = null;
            var validTiles = new List<Vector2i>();

            // Сканируем комнату
            for (var x = roomStartX; x <= roomEndX; x++)
            {
                for (var y = roomStartY; y <= roomEndY; y++)
                {
                    var posCoords = new Vector2i(startPos.X + roomIdx.X * stepX + x, startPos.Y + roomIdx.Y * stepY + y);
                    var pos = _mapSys.ToCoordinates(gridUid, posCoords, grid);
                    var entities = _mapSys.GetAnchoredEntities(gridUid, grid, pos);
                    if (!entities.Any())
                    {
                        validTiles.Add(posCoords);
                        continue;
                    }

                    foreach (var ent in entities)
                    {
                        if (HasComp<EntityStorageComponent>(ent) && _tag.HasTag(ent, _chestTag))
                        {
                            targetChest = ent;
                            break;
                        }
                    }
                    if (targetChest != null) break;
                }
                if (targetChest != null) break;
            }

            if (targetChest != null && TryComp<EntityStorageComponent>(targetChest, out var storage))
            {
                // Вариант 1: Сундук найден, кладем ключ внутрь
                var keyUid = Spawn(keyProto, Transform(targetChest.Value).Coordinates);
                _entityStorage.Insert(keyUid, targetChest.Value, storage);
            }
            else
            {
                // Вариант 2: Сундука нет, спавним на случайном тайле
                var spawnPos = new Vector2i(startPos.X + roomIdx.X * stepX + halfX, startPos.Y + roomIdx.Y * stepY + halfY); // По дефолту центр

                if (validTiles.Count > 0)
                {
                    spawnPos = _random.Pick(validTiles);
                }

                Spawn(keyProto, _mapSys.ToCoordinates(gridUid, spawnPos, grid));
            }
        }
    }

    private void GetPlayerSpawns(EntityUid gridUid, MapGridComponent grid, Vector2i startPos, List<Vector2i> roomCoords, out List<EntityCoordinates> toSpawn)
    {
        toSpawn = new();

        var stepX = _size.X - 1;
        var stepY = _size.Y - 1;
        var halfX = _size.X / 2;
        var halfY = _size.Y / 2;

        foreach (var roomIdx in roomCoords)
        {
            var roomStartX = startPos.X + roomIdx.X * stepX + 1;
            var roomStartY = startPos.Y + roomIdx.Y * stepY + 1;
            var roomEndX = roomStartX + stepX - 2;
            var roomEndY = roomStartY + stepY - 2;

            var validTiles = new List<Vector2i>();

            for (var x = roomStartX; x <= roomEndX; x++)
            {
                for (var y = roomStartY; y <= roomEndY; y++)
                {
                    var posCoords = new Vector2i(startPos.X + roomIdx.X * stepX + x, startPos.Y + roomIdx.Y * stepY + y);
                    var pos = _mapSys.ToCoordinates(gridUid, posCoords, grid);
                    var entities = _mapSys.GetAnchoredEntities(gridUid, grid, pos);
                    if (!entities.Any())
                    {
                        validTiles.Add(posCoords);
                    }
                }
            }
            var spawnPos = new Vector2i(startPos.X + roomIdx.X * stepX + halfX, startPos.Y + roomIdx.Y * stepY + halfY);
            if (validTiles.Count > 0)
            {
                spawnPos = _random.Pick(validTiles);
            }

            var spawnCoords = _mapSys.ToCoordinates(gridUid, spawnPos, grid);
            toSpawn.Add(spawnCoords);
        }
    }

    private void SpawnWall(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        if (_mapSys.GetAnchoredEntities(gridUid, grid, pos).Any()) return;
        var wallProto = _random.Pick(_wallProtoIds);
        Spawn(wallProto, _mapSys.ToCoordinates(gridUid, pos, grid));
    }

    private char GetLetter(int index) => (char)('A' + (index % 26));
}
