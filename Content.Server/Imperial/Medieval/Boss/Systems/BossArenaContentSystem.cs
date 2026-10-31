using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaContentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossArenaStateEvent>(OnArena);
        SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
    }

    public IEnumerable<BossArenaContentDefinitionComponent> Definitions(string arenaId)
    {
        return _prototypes.EnumeratePrototypes<EntityPrototype>()
            .Select(prototype => prototype.TryGetComponent<BossArenaContentDefinitionComponent>(out var definition, Factory)
                ? definition : null)
            .OfType<BossArenaContentDefinitionComponent>()
            .Where(definition => definition.ArenaId == arenaId)
            .GroupBy(definition => definition.Role)
            .Select(group => group.OrderByDescending(definition => definition.Priority).First()); // one provider per content slot
    }

    public EntProtoId? Resource(string arenaId, string role)
        => Definitions(arenaId).FirstOrDefault(definition => definition.Role == role)?.Prototype;

    private void OnArena(ref BossArenaStateEvent args)
    {
        if (args.State != BossArenaState.Loaded) return;
        var arena = args.Arena;
        if (!TryComp<MapGridComponent>(arena.Comp.CombatGrid, out var grid)) return;
        var state = EnsureComp<BossArenaContentStateComponent>(arena);
        foreach (var definition in Definitions(arena.Comp.ArenaId))
        {
            if (!state.Roles.Add(definition.Role)) continue;           // already added
            if (definition.SceneryFile is { } file && state.Scenery == null &&
                _loader.TryLoadGrid(Transform(arena).MapID, file, out var scenery))
                state.Scenery = scenery.Value.Owner;
            foreach (var decoration in definition.Decorations)
            {
                var parent = decoration.Scenery ? state.Scenery : arena.Comp.CombatGrid; // choose the outer grid or combat floor
                if (parent is { } uid)
                    Spawn(decoration.Prototype, new EntityCoordinates(uid, decoration.Position));
            }
            if (definition.Tiles.Count > 0)
                _maps.SetTiles(arena.Comp.CombatGrid, grid, definition.Tiles.Select(tile =>
                    (tile.Position, new Tile(_tiles[tile.Tile].TileId))).ToList());
        }
    }

    private void OnDefeated(ref BossDefeatedEvent args)
    {
        if (Transform(args.Boss).MapUid is not { } map ||
            !TryComp<BossArenaMapComponent>(map, out var arena) || arena.Boss != args.Boss) return;
        foreach (var definition in Definitions(arena.ArenaId))
            if (definition.DefeatComponents.Count > 0)
                EntityManager.AddComponents(args.Boss, definition.DefeatComponents);
    }
}
