using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossTraversalSystem : EntitySystem
{
    private const float CrossDashSeconds = 0.6f;
    private const float CrossLandingDelay = 0.5f;
    private const float CleaveDashSeconds = 0.65f;
    private const float EdgeDashSeconds = 0.8f;
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly BossArenaProtectionSystem _movement = default!;
    [Dependency] private readonly BossArenaMapSystem _maps = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BossSequenceSystem _sequences = default!;

    public override void Initialize()
        => SubscribeLocalEvent<BossTraversalComponent, ComponentShutdown>(OnShutdown);

    public static Vector2i DirectionTile(int direction) => direction switch
    {
        0 => new(0, 1),
        1 => new(1, 0),
        2 => new(0, -1),
        _ => new(-1, 0)
    };

    public static BossTelegraphPattern Cleave(int direction) => direction switch
    {
        0 => BossTelegraphPattern.Frons,
        1 => BossTelegraphPattern.Dextra,
        2 => BossTelegraphPattern.Tergum,
        _ => BossTelegraphPattern.Sinistra
    };

    private static string Cardinal(int direction) => direction switch
    {
        0 => "Septentrio",
        1 => "Oriens",
        2 => "Meridies",
        _ => "Occidens"
    };

    public bool TryStart(EntityUid boss, BossTraversalKind kind, DamageSpecifier damage,
        bool spread = false, bool flank = false, DamageSpecifier? spreadDamage = null,
        int rainCount = 4, int? direction = null, int? distance = null,
        float rainWarningTime = 3, IReadOnlyList<int>? rainSizes = null,
        float rainCastTime = 1.5f, int rainConcurrent = 8, float rainSpawnInterval = 0.5f, float rainDoubleChance = 0,
        int rainMaxConcurrent = 8, float rainMinSpawnInterval = 0.5f, float rainRampSeconds = 60,
        DamageSpecifier? flankDamage = null)
    {
        if (!HasComp<BossEncounterComponent>(boss) ||
            !TryComp<BossComponent>(boss, out var encounter) || !encounter.Active || encounter.Finished || encounter.Health <= 0 ||
            Terminating(boss) || MetaData(boss).EntityPaused || HasComp<BossTraversalComponent>(boss) ||
            HasComp<BossMechanicComponent>(boss) || HasComp<BossCastingComponent>(boss) || HasComp<BossSequenceComponent>(boss) ||
            !_telegraphs.TryGetArena(boss, out var arena) || arena.Comp.HalfSize is < 2 or > 32 || distance > arena.Comp.HalfSize ||
            _telegraphs.LivingPlayers(boss).Count == 0 || !ValidDamage(damage) || !Enum.IsDefined(kind) ||
            direction is < 0 or > 3 || distance is < 2 or > 3 || rainCount is < 1 or > 256 || rainConcurrent is < 1 or > 30 ||
            rainMaxConcurrent < rainConcurrent || rainMaxConcurrent > 30 ||
            !float.IsFinite(rainMinSpawnInterval) || rainMinSpawnInterval < 0.1f || rainMinSpawnInterval > rainSpawnInterval ||
            !float.IsFinite(rainRampSeconds) || rainRampSeconds is < 1 or > 600 ||
            !float.IsFinite(rainCastTime) || rainCastTime is < 0.5f or > 10 ||
            !float.IsFinite(rainSpawnInterval) || rainSpawnInterval is < 0.1f or > 5 ||
            !float.IsFinite(rainDoubleChance) || rainDoubleChance is < 0 or > 1 ||
            !float.IsFinite(rainWarningTime) || rainWarningTime is < 0.5f or > 10 ||
            rainSizes != null && (rainSizes.Count is < 1 or > 12 || rainSizes.Any(size => size is not (2 or 3))) ||
            ((spread || flank) && kind != BossTraversalKind.EdgeCone) ||
            spread && flank ||
            (spread && (spreadDamage == null || !ValidDamage(spreadDamage))) ||
            (flank && flankDamage != null && !ValidDamage(flankDamage))) return false;

        var selectedDirection = direction ?? (kind == BossTraversalKind.StepCleave
            ? _random.Pick(Enumerable.Range(0, 4).Where(candidate =>
                _sequences.CanFollowHistory(boss, [Cleave((candidate + 2) % 4)])).ToList())
            : _random.Next(4));
        if (kind == BossTraversalKind.StepCleave &&
            !_sequences.CanFollowHistory(boss, [Cleave((selectedDirection + 2) % 4)])) return false;

        var state = AddComp<BossTraversalComponent>(boss);
        state.Kind = kind;
        state.Arena = arena.Owner;
        state.Grid = arena.Comp.CombatGrid;
        state.Damage = new(damage);
        state.SpreadDamage = spreadDamage == null ? new() : new(spreadDamage);
        state.FlankDamage = new(flankDamage ?? damage);
        state.Direction = selectedDirection;
        state.Distance = kind == BossTraversalKind.EdgeCone ? arena.Comp.HalfSize + Math.Min(2, arena.Comp.HalfSize - 1) :
            kind == BossTraversalKind.StepCross ? Math.Min(4, arena.Comp.HalfSize) : distance ?? _random.Next(2, Math.Min(3, arena.Comp.HalfSize) + 1);
        state.Flank = flank;
        state.Pattern = kind switch
        {
            BossTraversalKind.StepCleave => Cleave((state.Direction + 2) % 4),
            BossTraversalKind.StepCross => BossTelegraphPattern.Cross,
            BossTraversalKind.EdgeCone => BossTelegraphPattern.CardinalCone,
            _ => BossTelegraphPattern.CrystalRain
        };
        if (spread)
            state.Targets = new BossTilePatternAttack { Telegraph = BossTelegraphPattern.Spread, SpreadTargets = 1 }
                .PickTargets(boss, _telegraphs.LivingPlayers(boss), EntityManager).ToList();
        if (kind == BossTraversalKind.CrystalRain)
        {
            state.Phase = BossTraversalPhase.Preparing;
            state.RemainingRain = rainCount;
            state.RainWarningTime = rainWarningTime;
            state.RainSizes = rainSizes?.ToList() ?? new() { 3 };
            state.RainConcurrent = rainConcurrent;
            state.RainSpawnInterval = rainSpawnInterval;
            state.RainDoubleChance = rainDoubleChance;
            state.RainMaxConcurrent = rainMaxConcurrent;
            state.RainMinSpawnInterval = rainMinSpawnInterval;
            state.RainRampSeconds = rainRampSeconds;
            state.Deadline = _timing.CurTime + TimeSpan.FromSeconds(rainCastTime);
            SetBar(boss, EntityManager.System<BossMechanicSystem>().NameKey(boss, "Crystal_Rain"), rainCastTime);
        }
        else
        {
            _maps.SetCenterBlocked(arena, false);
            state.Phase = BossTraversalPhase.Preparing;
            state.Deadline = _timing.CurTime + TimeSpan.FromSeconds(3);
            var name = kind switch
            {
                BossTraversalKind.StepCleave => EntityManager.System<BossMechanicSystem>().NameKey(boss, "Transitus_" + Cardinal((state.Direction + 2) % 4)),
                BossTraversalKind.StepCross => EntityManager.System<BossMechanicSystem>().NameKey(boss, "Moving_Cross"),
                _ => EntityManager.System<BossMechanicSystem>().NameKey(boss, "Edge_Cone_" + Cardinal(state.Direction))
            };
            SetBar(boss, name, kind == BossTraversalKind.StepCross ? 3 + CrossDashSeconds + CrossLandingDelay :
                kind == BossTraversalKind.EdgeCone ? 6 + EdgeDashSeconds : 6 + CleaveDashSeconds);
            PrepareWarning(boss, state, Path(state, 0, state.Distance));
        }
        return true;
    }

    private static bool ValidDamage(DamageSpecifier damage)
        => damage.AnyPositive() && damage.DamageDict.Values.All(value => value >= 0);

    private void SetBar(EntityUid boss, string name, float seconds)
    {
        var bar = EnsureComp<BossCastBarComponent>(boss);
        bar.Casting = bar.Visible = true;
        bar.Preparing = false;
        bar.Anchor = null;
        bar.SetCastName(name);
        bar.StartTime = _timing.CurTime;
        bar.EndTime = bar.StartTime + TimeSpan.FromSeconds(seconds);
        Dirty(boss, bar);
    }

    private static List<Vector2i> Path(BossTraversalComponent state, int from, int to)
    {
        var direction = DirectionTile(state.Direction);
        var sign = Math.Sign(to - from);
        return Enumerable.Range(0, Math.Abs(to - from) + 1).Select(i => direction * (from + sign * i)).ToList();
    }

    private void PrepareWarning(EntityUid boss, BossTraversalComponent state, List<Vector2i> path)
    {
        _telegraphs.PrepareTraversal(boss, state.Pattern, state.Targets, _timing.CurTime, state.Deadline,
            state.Origin, state.Pattern == BossTelegraphPattern.CardinalCone ? (4 - state.Direction) % 4 : 0,
            state.Phase == BossTraversalPhase.Returning ? [] : path,
            state.Pattern == BossTelegraphPattern.CardinalCone && state.Targets.Count > 0);
    }

    private void Move(EntityUid boss, BossTraversalComponent state, int position)
    {
        state.Position = position;
        state.Origin = DirectionTile(state.Direction) * position;
        _movement.MoveBoss(boss, new EntityCoordinates(state.Grid, state.Origin.X + 0.5f, state.Origin.Y + 0.5f),
            anchor: state.Kind == BossTraversalKind.EdgeCone || position == 0 ||
                state.Phase != BossTraversalPhase.Returning && position == state.Distance);
        _telegraphs.SetTraversalFootprint(boss, state.Origin,
            state.Pattern == BossTelegraphPattern.CardinalCone ? (4 - state.Direction) % 4 : 0,
            state.Phase == BossTraversalPhase.Returning ? [] : Path(state, position, state.Distance));
    }

    private void MoveAlongCurve(EntityUid boss, BossTraversalComponent state)
    {
        if (state.Phase is not (BossTraversalPhase.Dashing or BossTraversalPhase.Returning)) return;
        var duration = (state.Deadline - state.DashStart).TotalSeconds;
        var progress = duration <= 0 ? 1 : Math.Clamp((_timing.CurTime - state.DashStart).TotalSeconds / duration, 0, 1);
        var eased = (float)(progress * progress * (3 - 2 * progress));
        var distance = state.MotionFrom + eased * (state.MotionTo - state.MotionFrom);
        var direction = DirectionTile(state.Direction);
        _movement.MoveBoss(boss, new EntityCoordinates(state.Grid,
            new Vector2(0.5f) + new Vector2(direction.X, direction.Y) * distance), anchor: false);
        var tile = Math.Clamp((int)MathF.Floor(distance + 0.5f), 0, state.Distance);
        if (tile == state.Position) return;
        state.Position = tile;
        state.Origin = direction * tile;
        _telegraphs.SetTraversalFootprint(boss, state.Origin,
            state.Pattern == BossTelegraphPattern.CardinalCone ? (4 - state.Direction) % 4 : 0,
            state.Phase == BossTraversalPhase.Returning ? [] : Path(state, tile, state.Distance));
    }

    private void StartMotion(EntityUid boss, BossTraversalComponent state, bool returning)
    {
        state.Phase = returning ? BossTraversalPhase.Returning : BossTraversalPhase.Dashing;
        state.MotionFrom = returning ? state.Distance : 0;
        state.MotionTo = returning ? 0 : state.Distance;
        state.DashStart = _timing.CurTime;
        var duration = returning ? state.Kind == BossTraversalKind.StepCross ? 2f : 1.5f :
            state.Kind == BossTraversalKind.StepCross ? CrossDashSeconds :
            state.Kind == BossTraversalKind.EdgeCone ? EdgeDashSeconds : CleaveDashSeconds;
        state.Deadline = state.DashStart + TimeSpan.FromSeconds(duration);
        if (!returning && _telegraphs.GetTelegraph(boss) is { } warning)
        {
            warning.Comp.EndTime = state.Deadline + TimeSpan.FromSeconds(
                state.Kind == BossTraversalKind.StepCross ? CrossLandingDelay : 0);
            Dirty(warning);
        }
    }

    private void SpawnRainDrop(EntityUid boss, BossTraversalComponent state)
    {
        var size = _random.Pick(state.RainSizes);
        var pattern = size == 2 ? BossTelegraphPattern.CrystalRainSmall : BossTelegraphPattern.CrystalRain;
        var halfSize = Comp<BossArenaMapComponent>(state.Arena).HalfSize;
        var minimum = -halfSize + (size == 3 ? 1 : 0);
        var maximum = halfSize - 1;
        var occupied = state.RainDrops.Where(Exists).Select(drop => Comp<BossTelegraphComponent>(drop).PatternOrigin).ToHashSet();
        var candidates = Enumerable.Range(minimum, maximum - minimum + 1).SelectMany(y =>
            Enumerable.Range(minimum, maximum - minimum + 1).Select(x => new Vector2i(x, y)))
            .Where(tile => tile != state.Origin && !occupied.Contains(tile)).ToList();
        if (candidates.Count == 0) return;
        var next = _random.Pick(candidates);
        state.Origin = next;
        state.RainWave++;
        state.RemainingRain--;
        state.RainDrops.Add(_telegraphs.CreateIndependentWarning(boss, pattern, next,
            _timing.CurTime + TimeSpan.FromSeconds(state.RainWarningTime)));
        if (!_telegraphs.HasCrystalAnimation(boss, pattern) && Comp<BossTelegraphSettingsComponent>(boss).FallingCrystalEffect is { } effect)
            state.RainVisuals.Add(Spawn(effect, new EntityCoordinates(state.Grid, next.X + (size == 2 ? 1 : 0.5f), next.Y + (size == 2 ? 1 : 0.5f))));
    }

    private void UpdateRain(EntityUid boss, BossTraversalComponent state)
    {
        var pressure = Math.Clamp((_timing.CurTime - state.RainPressureStart).TotalSeconds / state.RainRampSeconds, 0, 1);
        state.RainActiveCap = (int)Math.Floor(state.RainConcurrent + pressure * (state.RainMaxConcurrent - state.RainConcurrent));
        state.RainCurrentInterval = (float)(state.RainSpawnInterval + pressure * (state.RainMinSpawnInterval - state.RainSpawnInterval));
        foreach (var drop in state.RainDrops.ToArray())
        {
            if (!TryComp<BossTelegraphComponent>(drop, out var warning)) { state.RainDrops.Remove(drop); continue; }
            if (_timing.CurTime < warning.EndTime) continue;
            state.RainResolved++;
            var impact = _telegraphs.ShowImpact(boss, warning.Danger, warningEntity: drop, playSound: state.RainResolved % 2 == 0);
            if (impact is { } visual && _telegraphs.HasCrystalAnimation(boss, warning.Pattern))
                state.RainVisuals.Add(visual);
            foreach (var player in EntityManager.AllEntities<MobStateComponent>().Select(entity => entity.Owner).ToArray())
            {
                if (!HasComp<BossTraversalComponent>(boss)) return;
                if (_telegraphs.TryGetPlayerTile(boss, player, out var tile, allowCritical: true) && warning.Danger.Contains(tile))
                    _damage.TryChangeDamage(player, state.Damage, origin: boss);
            }
            if (!HasComp<BossTraversalComponent>(boss)) return;
            if (!_telegraphs.HasCrystalAnimation(boss, warning.Pattern) && Comp<BossTelegraphSettingsComponent>(boss).CrystalShatterEffect is { } effect)
            {
                var offset = warning.Pattern == BossTelegraphPattern.CrystalRainSmall ? 1 : 0.5f;
                state.RainVisuals.Add(Spawn(effect, new EntityCoordinates(state.Grid, warning.PatternOrigin.X + offset, warning.PatternOrigin.Y + offset)));
            }
            if (Exists(drop) && !Terminating(drop)) Del(drop);
            state.RainDrops.Remove(drop);
        }
        if (state.RemainingRain > 0 && _timing.CurTime >= state.Deadline)
        {
            var count = _random.Prob(state.RainDoubleChance) ? 2 : 1;
            for (var i = 0; i < count && state.RemainingRain > 0 && state.RainDrops.Count < state.RainActiveCap; i++)
                SpawnRainDrop(boss, state);
            state.Deadline = _timing.CurTime + TimeSpan.FromSeconds(state.RainCurrentInterval);
        }
        if (state.RemainingRain == 0 && state.RainDrops.Count == 0)
        {
            state.Completed = true;
            RemComp<BossTraversalComponent>(boss);
        }
    }

    public bool Cancel(EntityUid boss)
    {
        if (Terminating(boss) || !TryComp<BossTraversalComponent>(boss, out var state) ||
            state.LifeStage >= ComponentLifeStage.Stopping)
            return false;
        RemComp<BossTraversalComponent>(boss);
        return true;
    }

    private void OnShutdown(EntityUid boss, BossTraversalComponent state, ComponentShutdown args)
    {
        foreach (var entity in state.RainDrops.Concat(state.RainVisuals))
        {

            if (state.Completed && TryComp<BossTelegraphComponent>(entity, out var visual) &&
                visual.Impact && BossRainCrystalVisuals.IsCrystal(visual.Pattern)) continue;
            if (Exists(entity) && !Terminating(entity)) Del(entity);
        }
        state.RainDrops.Clear();
        state.RainVisuals.Clear();
        _telegraphs.Clear(boss);
        if (Terminating(boss)) return;
        if (_telegraphs.TryGetArena(boss, out var arena) && arena.Owner == state.Arena && !Terminating(arena.Owner))
        {
            _movement.MoveBoss(boss, new EntityCoordinates(state.Grid, 0.5f, 0.5f));
            if (TryComp<BossComponent>(boss, out var encounter) && encounter.Active && !encounter.Finished && encounter.Health > 0)
                _maps.SetCenterBlocked(arena, true);
        }
        if (TryComp<BossCastBarComponent>(boss, out var bar))
        {
            bar.Casting = false;
            bar.Visible = true;
            bar.Anchor = null;
            bar.ClearCastName();
            bar.StartTime = bar.EndTime = TimeSpan.Zero;
            Dirty(boss, bar);
        }
        EntityManager.System<BossEncounterSystem>().StepFinished(boss, state.Completed);
    }

    private void Resolve(EntityUid boss, BossTraversalComponent state)
    {
        var danger = _telegraphs.SnapshotDanger(boss);
        var primary = BossTilePatterns.Danger(state.Pattern, [], facing:
            state.Pattern == BossTelegraphPattern.CardinalCone ? (4 - state.Direction) % 4 : 0, origin: state.Origin, halfSize: Comp<BossArenaMapComponent>(state.Arena).HalfSize);
        var targets = state.Targets.Where(target => _telegraphs.TryGetPlayerTile(boss, target, out _))
            .Select(target => { _telegraphs.TryGetPlayerTile(boss, target, out var tile); return tile; }).ToList();
        var spread = state.Pattern == BossTelegraphPattern.CardinalCone
            ? BossTilePatterns.Danger(BossTelegraphPattern.Spread, targets, halfSize: Comp<BossArenaMapComponent>(state.Arena).HalfSize) : new();
        var flank = state.FlankPattern is { } side
            ? BossTilePatterns.Danger(side, [], origin: state.Origin, halfSize: Comp<BossArenaMapComponent>(state.Arena).HalfSize) : new();
        var victims = EntityManager.AllEntities<MobStateComponent>().Select(player => player.Owner)
            .Where(player => _telegraphs.TryGetPlayerTile(boss, player, out var tile, allowCritical: true) && danger.Contains(tile)).ToArray();
        if (BossSequenceSystem.IsCleave(state.Pattern)) _sequences.RecordStrike(boss, state.Pattern);
        if (state.FlankPattern is { } resolvedSide) _sequences.RecordStrike(boss, resolvedSide);
        _telegraphs.ShowImpact(boss, danger, additionalSlash: state.FlankPattern != null);
        foreach (var player in victims)
        {
            if (!HasComp<BossTraversalComponent>(boss)) break;
            _telegraphs.TryGetPlayerTile(boss, player, out var tile, allowCritical: true);
            var damage = primary.Contains(tile) ? new DamageSpecifier(state.Damage) : new();
            if (spread.Contains(tile)) damage += state.SpreadDamage;
            if (flank.Contains(tile)) damage += state.FlankDamage;
            _damage.TryChangeDamage(player, damage, origin: boss);
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BossTraversalComponent>();
        while (query.MoveNext(out var boss, out var state))
        {
            if (MetaData(boss).EntityPaused) continue;
            if (!TryComp<BossComponent>(boss, out var encounter) || !encounter.Active || encounter.Finished || encounter.Health <= 0 ||
                !_telegraphs.TryGetArena(boss, out var arena) || arena.Owner != state.Arena || arena.Comp.CombatGrid != state.Grid ||
                _telegraphs.LivingPlayers(boss).Count == 0 ||
                state.Targets.Count > 0 && !state.Targets.Any(target => _telegraphs.TryGetPlayerTile(boss, target, out _)))
            { Cancel(boss); continue; }
            if (state.Kind == BossTraversalKind.CrystalRain)
            {
                if (state.Phase == BossTraversalPhase.Preparing && _timing.CurTime >= state.Deadline)
                {
                    state.Phase = BossTraversalPhase.Rain;
                    var rotation = Comp<BossEncounterComponent>(boss);
                    rotation.RainPressureStart ??= _timing.CurTime;
                    state.RainPressureStart = rotation.RainPressureStart.Value;
                    var bar = Comp<BossCastBarComponent>(boss);
                    bar.Casting = false;
                    bar.ClearCastName();
                    Dirty(boss, bar);
                }
                if (state.Phase == BossTraversalPhase.Rain) UpdateRain(boss, state);
                continue;
            }
            MoveAlongCurve(boss, state);
            _telegraphs.SnapshotDanger(boss);
            if (_timing.CurTime < state.Deadline) continue;
            if (state.Phase == BossTraversalPhase.Preparing)
            {
                StartMotion(boss, state, returning: false);
                if (state.Kind == BossTraversalKind.StepCross)
                {
                    var bar = Comp<BossCastBarComponent>(boss);
                    bar.EndTime = state.Deadline + TimeSpan.FromSeconds(CrossLandingDelay);
                    Dirty(boss, bar);
                }
                continue;
            }
            if (state.Phase == BossTraversalPhase.Dashing)
            {
                Move(boss, state, state.Distance);
                state.Phase = BossTraversalPhase.Strike;
                state.Deadline = _timing.CurTime + TimeSpan.FromSeconds(
                    state.Kind == BossTraversalKind.StepCross ? CrossLandingDelay : 3);
                if (_telegraphs.GetTelegraph(boss) is { } warning)
                {
                    warning.Comp.EndTime = state.Deadline;
                    Dirty(warning);
                }
                var bar = Comp<BossCastBarComponent>(boss);
                bar.EndTime = state.Deadline;
                Dirty(boss, bar);
                if (state.Flank)
                {
                    var side = _random.Pick(Enumerable.Range(0, 4).Where(candidate =>
                        candidate % 2 != state.Direction % 2 &&
                        _sequences.CanFollowHistory(boss, [Cleave(candidate)])).ToList());
                    state.FlankPattern = Cleave(side);
                    _telegraphs.SetSimultaneousPattern(boss, state.FlankPattern.Value, state.Origin, facing: 0);
                    SetBar(boss, EntityManager.System<BossMechanicSystem>().NameKey(boss, "Edge_Slash_" + Cardinal(side)), 3);
                }
                continue;
            }
            if (state.Phase == BossTraversalPhase.Returning)
            {
                Move(boss, state, 0);
                state.Completed = true;
                RemComp<BossTraversalComponent>(boss);
                continue;
            }
            Resolve(boss, state);
            if (!HasComp<BossTraversalComponent>(boss)) continue;
            state.Pattern = BossTelegraphPattern.TravelOnly;
            StartMotion(boss, state, returning: true);
            var finishedBar = Comp<BossCastBarComponent>(boss);
            finishedBar.Casting = false;
            finishedBar.ClearCastName();
            Dirty(boss, finishedBar);
            PrepareWarning(boss, state, Path(state, state.Position, 0));
        }
    }
}
