using Content.Shared.Damage;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Ships.Wave;

/// <summary>
/// Спавнит волны, щиииииткод
/// </summary>
public sealed class SpawnWaveSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnWaveComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, SpawnWaveComponent component, ComponentInit args)
    {
        var entcoords = _transform.GetMoverCoordinates(uid);
        var mapcoords = _transform.GetMapCoordinates(uid);
        var grid = _mapManager.CreateGridEntity(mapcoords.MapId);
        _transform.SetCoordinates(grid, entcoords);
        EnsureComp<WaveComponent>(grid);
        _tileDefinitionManager.TryGetDefinition("FloorWood", out var tileDefinition);// сюда поставить воду
        if (tileDefinition == null)
            return;
        _map.SetTile(grid, new Vector2i(0,0),new Tile(tileDefinition.TileId, 0, 0));// создаёт тайлик воды надо поставить воду вон туда
        _entityManager.DeleteEntity(uid);
    }
}
