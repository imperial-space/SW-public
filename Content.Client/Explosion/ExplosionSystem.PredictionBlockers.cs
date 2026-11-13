// Imperial medieval start
using Content.Shared.Atmos;
using Content.Shared.Explosion.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;

namespace Content.Client.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    // Keep grid-local tiles so moving or rotating the entire grid needs no cache rebuild.
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile)> _predictionBlockerTiles = new();
    private readonly HashSet<EntityUid> _changedPredictionBlockers = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _dirtyPredictionBlockerTiles = new();

    private void InitializePredictionBlockers()
    {
        SubscribeLocalEvent<ExplosionBlockerComponent, ComponentStartup>(OnPredictionBlockerStartup);
        SubscribeLocalEvent<ExplosionBlockerComponent, AfterAutoHandleStateEvent>(OnPredictionBlockerState);
        SubscribeLocalEvent<ExplosionBlockerComponent, ComponentShutdown>(OnPredictionBlockerShutdown);
        SubscribeLocalEvent<ExplosionBlockerComponent, MoveEvent>(OnPredictionBlockerMove);
        SubscribeLocalEvent<ExplosionBlockerComponent, AnchorStateChangedEvent>(OnPredictionBlockerAnchor);
    }

    // Transform and blocker states can arrive in either order. Read their final values only after state application.
    private void OnPredictionBlockerStartup(Entity<ExplosionBlockerComponent> ent, ref ComponentStartup args)
        => _changedPredictionBlockers.Add(ent.Owner);

    private void OnPredictionBlockerState(Entity<ExplosionBlockerComponent> ent, ref AfterAutoHandleStateEvent args)
        => _changedPredictionBlockers.Add(ent.Owner);

    private void OnPredictionBlockerShutdown(Entity<ExplosionBlockerComponent> ent, ref ComponentShutdown args)
        => _changedPredictionBlockers.Add(ent.Owner);

    private void OnPredictionBlockerMove(Entity<ExplosionBlockerComponent> ent, ref MoveEvent args)
        => _changedPredictionBlockers.Add(ent.Owner);

    private void OnPredictionBlockerAnchor(Entity<ExplosionBlockerComponent> ent, ref AnchorStateChangedEvent args)
        => _changedPredictionBlockers.Add(ent.Owner);

    private void RefreshPredictionBlockers()
    {
        foreach (var uid in _changedPredictionBlockers)
        {
            // Always invalidate the old tile, including when an entity leaves PVS or loses its component.
            if (_predictionBlockerTiles.Remove(uid, out var oldTile))
                _dirtyPredictionBlockerTiles.Add(oldTile);

            if (TerminatingOrDeleted(uid) ||
                !TryComp<ExplosionBlockerComponent>(uid, out var blocker) ||
                blocker.LifeStage is < ComponentLifeStage.Starting or >= ComponentLifeStage.Stopping ||
                !TryComp<TransformComponent>(uid, out var xform) || !xform.Anchored ||
                !TryComp<MapGridComponent>(xform.GridUid, out var grid))
                continue;

            var gridUid = xform.GridUid!.Value;
            var tile = (gridUid, _map.CoordinatesToTile(gridUid, grid, xform.Coordinates));
            _predictionBlockerTiles[uid] = tile;
            _dirtyPredictionBlockerTiles.Add(tile);
        }
        _changedPredictionBlockers.Clear();

        foreach (var (gridUid, tile) in _dirtyPredictionBlockerTiles)
            RefreshPredictionBlockerTile(gridUid, tile);
        _dirtyPredictionBlockerTiles.Clear();
    }

    private void RefreshPredictionBlockerTile(EntityUid gridUid, Vector2i tile)
    {
        if (TerminatingOrDeleted(gridUid) || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _airtightMap.Remove(gridUid);
            return;
        }

        var directions = AtmosDirection.Invalid;
        float[]? tolerance = null;
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TerminatingOrDeleted(uid.Value) ||
                !TryComp<ExplosionBlockerComponent>(uid.Value, out var blocker) ||
                blocker.LifeStage is < ComponentLifeStage.Starting or >= ComponentLifeStage.Stopping ||
                blocker.BlockedDirections == AtmosDirection.Invalid)
                continue;

            directions |= blocker.BlockedDirections;
            tolerance ??= new float[_explosionTypes.Count];
            for (var i = 0; i < tolerance.Length; i++)
                tolerance[i] = MathF.Max(tolerance[i],
                    blocker.Tolerance.Length == tolerance.Length ? blocker.Tolerance[i] : float.MaxValue);
        }

        if (directions == AtmosDirection.Invalid)
        {
            if (_airtightMap.TryGetValue(gridUid, out var existing))
                existing.Remove(tile);
            return;
        }

        if (!_airtightMap.TryGetValue(gridUid, out var tiles))
            _airtightMap[gridUid] = tiles = new();
        tiles[tile] = new(tolerance!, directions);
    }

    public override void Shutdown()
    {
        _changedPredictionBlockers.Clear();
        _predictionBlockerTiles.Clear();
        _dirtyPredictionBlockerTiles.Clear();
        _airtightMap.Clear();
        base.Shutdown();
    }
}
// Imperial medieval end
