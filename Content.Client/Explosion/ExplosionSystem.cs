using Content.Shared.Explosion.EntitySystems;
// Imperial medieval start
using System.Linq;
using System.Numerics;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Atmos;
using Content.Shared.Projectiles;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
// Imperial medieval end

namespace Content.Client.Explosion.EntitySystems;

// Imperial medieval start
public sealed partial class ExplosionSystem : SharedExplosionSystem
{
    [Dependency] private readonly IClientGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly Dictionary<ProjectilePredictionKey, (EntityUid? Visual, TimeSpan Time)> _predicted = new();
    private readonly Dictionary<ProjectilePredictionKey, TimeSpan> _heard = new();
    protected override bool RejectTruncatedFlood => true;

    public override void Initialize()
    {
        base.Initialize();
        InitializePredictionBlockers(); // Imperial medieval edit - maintain the cache from blocker changes.
        SubscribeNetworkEvent<ExplosionSoundEvent>(OnSound);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetach);
    }

    public override void PredictProjectileExplosion(EntityUid uid, ProjectilePredictionKey key)
    {
        if (!_timing.IsFirstTimePredicted || !IsClientSide(uid) || _predicted.ContainsKey(key) ||
            !TryComp<ExplosiveComponent>(uid, out var explosive) ||
            !_prototypes.TryIndex(explosive.ExplosionType, out var prototype))
            return;
        var epicenter = _transform.GetMapCoordinates(uid);
        if (!_map.MapExists(epicenter.MapId))
            return;
        var visuals = GetPredictedVisuals(epicenter, explosive.ExplosionType, explosive.TotalIntensity,
            explosive.IntensitySlope, explosive.MaxIntensity);
        if (visuals == null)
            return;
        var entity = Spawn(null, MapCoordinates.Nullspace);
        EnsureComp<AppearanceComponent>(entity); // Imperial medieval edit - required for predicted fire overlay.
        AddComp(entity, visuals);
        _appearance.SetData(entity, ExplosionAppearanceData.Progress, int.MaxValue);
        // Ordinary small explosions persist for one second. Authoritative visuals take over as soon as they arrive.
        AddComp(entity, new TimedDespawnComponent { Lifetime = 1f });
        _predicted[key] = (entity, _timing.RealTime);
        var sound = visuals.Intensity.Count < prototype.SmallSoundIterationThreshold ? prototype.SmallSound : prototype.Sound;
        _audio.PlayStatic(sound, Filter.Local(), _transform.ToCoordinates(_map.GetMap(epicenter.MapId), epicenter), true, sound.Params);
        _heard[key] = _timing.RealTime;
    }

    /// <summary>Native tile propagation with replicated blockers, without gameplay processing.</summary>
    public ExplosionVisualsComponent? GetPredictedVisuals(MapCoordinates epicenter, string type,
        float total, float slope, float maxIntensity)
    {
        if (!float.IsFinite(total) || !float.IsFinite(slope) || !float.IsFinite(maxIntensity) ||
            total <= 0 || slope <= 0 || maxIntensity <= 0 || !_map.MapExists(epicenter.MapId))
            return null;
        var radius = IntensityToRadius(total, slope, maxIntensity);
        if (!float.IsFinite(radius) || radius > FloodMaxIterations / 4)
            return null;
        RefreshPredictionBlockers(); // Imperial medieval edit - update only dirty tiles, across all relevant grids.
        var result = GetExplosionTiles(epicenter, type, total, slope, maxIntensity);
        if (result == null)
            return null;
        var (_, intensity, space, grids, matrix) = result.Value;
        return new ExplosionVisualsComponent
        {
            Epicenter = epicenter, ExplosionType = type, Intensity = intensity,
            SpaceTiles = space?.TileLists, SpaceTileSize = space?.TileSize ?? DefaultTileSize,
            SpaceMatrix = matrix, Tiles = grids.ToDictionary(pair => pair.Key, pair => pair.Value.TileLists),
        };
    }
    /// <summary>Hand over to the native wall-aware overlay without displaying two copies.</summary>
    public void ReconcileVisuals(List<ExplosionPrediction> predictions)
    {
        foreach (var prediction in predictions)
        {
            if (!TryGetEntity(prediction.Shooter, out var shooter) || shooter != _player.LocalEntity)
                continue;
            if (_predicted.TryGetValue(prediction.Key, out var previous) && previous.Visual is { } entity && Exists(entity))
                Del(entity);
            _predicted[prediction.Key] = (null, _timing.RealTime);
        }
    }

    private void OnSound(ExplosionSoundEvent args)
    {
        if (args.Keys.All(key => _heard.ContainsKey(key)))
            return;
        foreach (var key in args.Keys)
            _heard[key] = _timing.RealTime;
        if (args.Global)
            _audio.PlayGlobal(args.Sound, Filter.Local(), true, args.Sound.Params);
        else if (_map.MapExists(args.Epicenter.MapId))
            _audio.PlayStatic(args.Sound, Filter.Local(), _transform.ToCoordinates(_map.GetMap(args.Epicenter.MapId), args.Epicenter), true, args.Sound.Params);
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        RefreshPredictionBlockers(); // Imperial medieval edit - also release removed blockers when no explosion is firing.
        foreach (var (key, value) in _predicted.ToArray())
            if (_timing.RealTime - value.Time > TimeSpan.FromSeconds(10))
                _predicted.Remove(key);
        foreach (var (key, time) in _heard.ToArray())
            if (_timing.RealTime - time > TimeSpan.FromSeconds(10))
                _heard.Remove(key);
    }

    private void OnDetach(LocalPlayerDetachedEvent args)
    {
        foreach (var value in _predicted.Values)
            if (value.Visual is { } entity && Exists(entity))
                QueueDel(entity);
        _predicted.Clear();
        _heard.Clear();
    }
}
// Imperial medieval end
