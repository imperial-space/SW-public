using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossArenaGenerationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public bool TryCreate(ProtoId<BossArenaPrototype> id, out Entity<BossArenaMapComponent> arena)
    {
        arena = default;
        if (!_prototypes.TryIndex(id, out var definition) || !IsValid(definition))
            return false;

        var map = _maps.CreateMap(out var mapId, runMapInit: false);
        var complete = false;
        try
        {
            SetEnvironment(map);
            var grid = CreatePlatform(mapId, definition);
            arena = ConfigureArena(map, grid, definition);
            EntityManager.System<BossSummoningTriggerSystem>().Restore(arena);
            _maps.InitializeMap(mapId);
            _maps.SetPaused(mapId, false);
            complete = true;
            return true;
        }
        finally
        {
            if (!complete)
                _maps.DeleteMap(mapId);
        }
    }

    private void SetEnvironment(EntityUid map)
    {
        AddComp(map, new GravityComponent { Enabled = true, Inherent = true });
        var air = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        air.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
        air.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
        _atmosphere.SetMapAtmosphere(map, false, air);
    }

    private EntityUid CreatePlatform(MapId map, BossArenaPrototype definition)
    {
        var grid = _mapManager.CreateGridEntity(map);
        var floor = _prototypes.Index(definition.Floor);
        var tiles = new List<(Vector2i, Tile)>();
        foreach (var tile in BossTilePatterns.GetPlatform(definition.HalfSize))
            tiles.Add((tile, new Tile(floor.TileId)));
        _maps.SetTiles(grid.Owner, grid.Comp, tiles);
        return grid.Owner;
    }

    private Entity<BossArenaMapComponent> ConfigureArena(EntityUid map, EntityUid grid, BossArenaPrototype definition)
    {
        var arrival = definition.ArrivalTile;
        var entrance = Spawn(definition.EntrancePrototype, new EntityCoordinates(grid, arrival.X + 0.5f, arrival.Y + 0.5f));
        var component = new BossArenaMapComponent
        {
            ArenaId = definition.ID,
            ArenaPrototype = definition.ID,
            BossPrototype = definition.BossPrototype,
            HalfSize = definition.HalfSize,
            Entrance = entrance,
            CombatGrid = grid,
            SummoningPrototype = definition.SummoningPrototype,
            CenterBlockerPrototype = definition.CenterBlockerPrototype,
            BrazierPrototype = definition.BrazierPrototype,
            VictoryExitPrototype = definition.VictoryExitPrototype,
            VictoryExitTile = definition.ExitTile,
            MaxPlayers = definition.MaxPlayers,
            ReplenishQuivers = definition.ReplenishQuivers,
            MessagePrefix = definition.MessagePrefix,
            AmbientLight = definition.AmbientLight,
            VictoryLootDuration = definition.VictoryLootDuration
        };
        AddComp(map, component);
        return new(map, component);
    }
}
