using Content.Server.MagicBarrier.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Ships.Sea.Init;

/// <summary>
/// по идее должно работать с матрицой моря
/// грёбаные костыли, я только примерно понимаю что делаю но вроде как всё нормально(нет я не пишу нейронкой, просто не особо понимаю, райдер помогает исправлять слишком грубые ошибки)
/// </summary>
public sealed class SeaMatrixInitSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MagicBarrierComponent, ComponentInit>(OnInit);
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

        if (component.SeaInitalazed)
            return;

        var seamat = component.SeaMatrix;

        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                if (!seamat.NeedsGeneration(x, y))
                {
                    if (seamat.GetCell(x, y).SeaId == new MapId(1))
                    {
                        seamat.SetSeaId(x, y, TryFoundMap(x,y));
                    }
                }
                var mapUid = _map.CreateMap();
                var mapId = _transform.GetMapId(mapUid);
                seamat.SetSeaId(x,y,mapId);
                seamat.SetGenerated(x,y,true);

            }
        }


    }

    /// <summary>
    /// ищем мапу либо -1 пишем
    /// </summary>
    public MapId TryFoundMap(int x, int y)
    {
        if (_entManager == null)
            throw new InvalidOperationException("EntityManager not initialized!");
        foreach (var notSeaComponent in _entManager.EntityQuery<NotSeaComponent>())
        {
            if (notSeaComponent.NotSeaPosX != x || notSeaComponent.NotSeaPosY != y)
                continue;

            return _transform.GetMapId(notSeaComponent.Owner);
        }
        return new MapId(-1);
    }






}
public sealed class SeaMatrix
{

    private readonly SeaCell[,] _matrix = new SeaCell[5, 5]; // 5x5 матрица


    public SeaMatrix(IEnumerable<(int x, int y)>? nonGeneratableCoordinates = null)
    {

        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                _matrix[x, y] = new SeaCell
                {
                    SeaId = new MapId(-1),
                    NeedGenerate = true
                };
            }
        }


        if (nonGeneratableCoordinates != null)
        {
            foreach (var (x, y) in nonGeneratableCoordinates)
            {
                if (x >= 0 && x < 5 && y >= 0 && y < 5)
                {
                    _matrix[x, y].NeedGenerate = false;
                }
            }
        }
    }


    /// <summary>
    /// Получение ячейки по координатам
    /// </summary>
    public SeaCell GetCell(int x, int y)
    {
        if (x < 0 || x >= 5 || y < 0 || y >= 5)
            throw new ArgumentOutOfRangeException("Coordinates out of 5x5 range!");

        return _matrix[x, y];
    }
    /// <summary>
    /// Установка ID моря
    /// </summary>
    public void SetSeaId(int x, int y, MapId seaId)
    {
        if (x < 0 || x >= 5 || y < 0 || y >= 5)
            throw new ArgumentOutOfRangeException("Coordinates out of 5x5 range!");

        _matrix[x, y].SeaId = seaId;
        _matrix[x, y].NeedGenerate = false; // Сбрасываем флаг после назначения ID
    }

    /// <summary>
    /// Проверка, нужно ли генерировать море в ячейке
    /// </summary>
    public bool NeedsGeneration(int x, int y)
    {
        return GetCell(x, y).NeedGenerate;
    }
    /// <summary>
    /// Ставим значение ячейке
    /// </summary>
    public void SetGenerated(int x, int y, bool generated)
    {
        _matrix[x, y].NeedGenerate = generated;
    }
}
public struct SeaCell
{
    public MapId SeaId;          // Уникальный ID моря
    public bool NeedGenerate;  // Флаг, что море нужно сгенерировать
}
