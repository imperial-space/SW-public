using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Medieval.Ships.Sea.Init;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Parallax;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Sea.Generation;

public sealed class SeasGenerationSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SeaMatrixInitSystem _seaMatrix = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private const int MapMin = -75;
    private const int MapMax = 75;

    // Используем прототипы вместо строк
    private static readonly (string PrototypeId, int Count)[] IslandConfig = {
        ("PirateIsland", 1),   // 1 большой
        ("FrendlyIslands", 2),   // 2 средних
        ("VolcanicIsland", 10)    // 10 мелких
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<MagicBarrierComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SeasGenerationEvent>(OnSeasGeneration);
    }

    private void OnInit(EntityUid uid, MagicBarrierComponent component, ComponentInit args)
    {
        if (component.SeaMatrix == null)
            component.SeaMatrix = new SeaMatrix(new List<(int x, int y)>
            {
                (2, 2), (2, 3), (2, 4),
                (3, 2), (3, 3), (3, 4),
                (4, 2), (4, 3), (4, 4),
            });

        if (component.SeaInitalazed) return;

        var seaMatrix = component.SeaMatrix;

        // Создаем 25 карт моря (5x5)
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                if (!seaMatrix.NeedsGeneration(x, y))
                    continue;

                var mapUid = _map.CreateMap();
                _metaData.SetEntityName(mapUid, $"Море {x} {y}");
                var parallax = AddComp<ParallaxComponent>(mapUid);
                parallax.Parallax = "OceanMedieval";
                var mapId = _transform.GetMapId(mapUid);
                AddComp<SeaComponent>(mapUid);
                seaMatrix.SetSeaId(x, y, mapId);
                seaMatrix.SetGenerated(x, y, true);
            }
        }

        // ✅ ГЕНЕРИРУЕМ ОСТРОВА С ИСПОЛЬЗОВАНИЕМ IPrototypeManager
        GenerateIslandsOnSeaMaps(seaMatrix);

        component.SeaInitalazed = true;
    }

    /// <summary>
    /// Генерирует острова, используя IPrototypeManager и конфигурацию по ID.
    /// Все острова размещаются в общем пространстве [-75, 75], без пересечений.
    /// </summary>
    private void GenerateIslandsOnSeaMaps(SeaMatrix seaMatrix)
    {
        var generatedObjects = new List<EntityUid>();
        var occupiedTiles = new HashSet<(int X, int Y)>();

        // Собираем все MapId карт моря
        var seaMapIds = new List<MapId>();
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                var cell = seaMatrix.GetCell(x, y);
                if (cell.NeedGenerate && !(cell.SeaId == new MapId(-1)))
                    seaMapIds.Add(cell.SeaId);
            }
        }

        if (seaMapIds.Count == 0)
        {
            Logger.Warning("No sea maps found to generate islands on!");
            return;
        }

        // Проходим по конфигурации островов
        foreach (var (prototypeId, count) in IslandConfig)
        {
            // Проверяем, существует ли прототип
            if (!_prototypeManager.TryIndex<IslandPrototype>(prototypeId, out var prototype) || prototype.Path == null)
            {
                Logger.Warning($"Island prototype '{prototypeId}' not found! Skipping.");
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                int attempts = 0;
                const int maxAttempts = 100;

                while (++attempts <= maxAttempts)
                {
                    // Выбираем случайную карту моря
                    var targetMapId = seaMapIds[_random.Next(seaMapIds.Count)];

                    // Случайная позиция на карте
                    int x = _random.Next(MapMin, MapMax - prototype.Size + 1);
                    int y = _random.Next(MapMin, MapMax - prototype.Size + 1);

                    // Проверяем пересечения
                    bool overlaps = false;
                    var newTiles = new List<(int X, int Y)>();

                    for (int dx = 0; dx < prototype.Size; dx++)
                    {
                        for (int dy = 0; dy < prototype.Size; dy++)
                        {
                            var tile = (x + dx, y + dy);
                            if (occupiedTiles.Contains(tile))
                            {
                                overlaps = true;
                                break;
                            }
                            newTiles.Add(tile);
                        }
                        if (overlaps)
                            break;
                    }

                    if (!overlaps)
                    {
                        _mapLoader.TryLoadGrid(targetMapId, new ResPath(prototype.Path), out var newObj, offset: new Vector2(x,y));
                        if (newObj.HasValue)
                        {
                            generatedObjects.Add(newObj.Value);
                            foreach (var tile in newTiles)
                                occupiedTiles.Add(tile);
                            break; // Успешно
                        }
                    }
                }

                if (attempts > maxAttempts)
                {
                    Logger.Warning($"Failed to generate {prototypeId} after {maxAttempts} attempts.");
                }
            }
        }

        Logger.Info($"Successfully generated {generatedObjects.Count} islands across {seaMapIds.Count} sea maps.");
    }

    public sealed class SeasGenerationEvent
    {
        public MapId MapId { get; set; }
        public int Count { get; set; }
        public string Prototype { get; set; } = "Reef";
    }

    private void OnSeasGeneration(SeasGenerationEvent ev)
    {
        // Оставлено для будущего расширения
    }
}
