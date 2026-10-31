using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossTelegraphSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedBossArenaRulesSystem _rules = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private sealed class Preparation
    {
        public EntityUid Root;
        public List<EntityUid> Targets = new();
        public Dictionary<EntityUid, EntityUid> Marks = new();
        public Dictionary<bool, List<EntityUid>> Emitters = new();
        public bool SequenceOwned;
        public bool MechanicOwned;
        public bool SequentialConstellation;
        public bool TraversalOwned;
        public BossTelegraphPattern? SimultaneousPattern;
        public Vector2i SimultaneousOrigin;
        public int? SimultaneousFacing;
    }
    private readonly Dictionary<EntityUid, Preparation> _prepared = new();      // one warning setup per boss, fights don't share this
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossTelegraphSettingsComponent, EntityTerminatingEvent>(OnBossTerminating);
        SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
        SubscribeLocalEvent<BossWonEvent>(OnWon);
        SubscribeLocalEvent<BossActorMarkerComponent, ContainerGettingInsertedAttemptEvent>(OnMarkerInsert);
    }

    private void OnMarkerInsert(EntityUid uid, BossActorMarkerComponent comp, ContainerGettingInsertedAttemptEvent args) => args.Cancel();
    private void CleanupEncounter(EntityUid boss)
    {
        Clear(boss);
        EntityManager.System<BossFocusMarkerSystem>().Clear(boss);
        foreach (var effect in EntityManager.AllEntities<BossTelegraphComponent>().ToArray())
            if (effect.Comp.Impact && effect.Comp.Boss == boss && Exists(effect.Owner) && !Terminating(effect.Owner))
                Del(effect.Owner);
    }
    private void OnBossTerminating(EntityUid uid, BossTelegraphSettingsComponent comp, ref EntityTerminatingEvent args) => CleanupEncounter(uid);
    private void OnDefeated(ref BossDefeatedEvent args)
    {
        if (HasComp<BossTelegraphSettingsComponent>(args.Boss))
            CleanupEncounter(args.Boss);
    }
    private void OnWon(ref BossWonEvent args)
    {
        if (HasComp<BossTelegraphSettingsComponent>(args.Boss))
            CleanupEncounter(args.Boss);
    }

    public bool TryGetArena(EntityUid boss, out Entity<BossArenaMapComponent> arena)
    {
        arena = default;
        if (!Exists(boss) || Transform(boss).MapUid is not { } map ||
            !TryComp<BossArenaMapComponent>(map, out var component) || component.Boss != boss || !Exists(component.CombatGrid))
            return false;
        arena = new(map, component);
        return true;
    }

    public bool TryGetPlayerTile(EntityUid boss, EntityUid player, out Vector2i tile, bool allowCritical = false)
    {
        tile = default;
        if (!Exists(player) || !_rules.IsPartyBody(player) ||
            (allowCritical ? _mobState.IsDead(player) : !_mobState.IsAlive(player)))
            return false;
        return TryGetPlatformTile(boss, player, out tile);
    }

    public bool TryGetPlatformTile(EntityUid boss, EntityUid player, out Vector2i tile)
    {
        tile = default;
        if (!TryGetArena(boss, out var arena) || !Exists(player) ||
            Transform(player).MapUid != arena.Owner || Transform(player).GridUid != arena.Comp.CombatGrid)
            return false;
        var local = Vector2.Transform(_transform.GetWorldPosition(player), _transform.GetInvWorldMatrix(arena.Comp.CombatGrid));
        tile = new(
            (int)MathF.Floor(local.X),                                          // Arena X
            (int)MathF.Floor(local.Y));                                         // Arena Y
        return BossTilePatterns.IsPlatform(tile, arena.Comp.HalfSize);
    }

    public List<EntityUid> LivingPlayers(EntityUid boss)
        => EntityManager.AllEntities<Content.Shared.Mobs.Components.MobStateComponent>()
            .Where(entity => TryGetPlayerTile(boss, entity.Owner, out _)).Select(entity => entity.Owner).OrderBy(uid => uid.Id).ToList();

    public bool Prepare(EntityUid boss, BossAttack attack, IReadOnlyList<EntityUid> selectedTargets, bool showTargetMarkers = true)
    {
        if (attack.Telegraph is not { } pattern || !TryComp<BossTelegraphSettingsComponent>(boss, out var settings) ||
            !TryGetArena(boss, out var arena) || !TryComp<Content.Shared.Imperial.Medieval.Boss.BossCastBarComponent>(boss, out var bar))
            return false;
        Clear(boss);
        var targets = selectedTargets.Where(target => TryGetPlayerTile(boss, target, out _)).Distinct().ToList();
        if (pattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack && targets.Count == 0)
            return false;
        if (pattern == BossTelegraphPattern.Spread)
            targets = targets.Take(4).ToList();
        if (pattern == BossTelegraphPattern.Stack)
            targets = targets.Take(1).ToList();

        var root = Spawn(settings.TelegraphPrototype, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
        var component = Comp<BossTelegraphComponent>(root);
        component.Style = EntityManager.System<BossMechanicDefinitionSystem>().Style(boss, pattern) ?? settings.Style;
        component.Boss = boss;
        component.Grid = arena.Comp.CombatGrid;
        component.HalfSize = arena.Comp.HalfSize;
        component.Pattern = pattern;
        component.SuppressDanger = _prototypes.TryIndex(component.Style, out var visualStyle) &&
            BossVigilVisuals.Enabled(visualStyle, pattern);

        var relativeRotation = _transform.GetWorldRotation(boss) - _transform.GetWorldRotation(arena.Comp.CombatGrid);
        component.Facing = settings.FixedCardinals ? 0 : (int)Math.Round(relativeRotation.Degrees / 90) % 4;
        component.StartTime = bar.StartTime;
        component.AnimationStartTime = component.StartTime;
        component.EndTime = bar.EndTime;
        var duration = bar.EndTime - bar.StartTime;
        component.SwitchTime = bar.StartTime + TimeSpan.FromTicks(duration.Ticks * 2 / 3);
        var sequentialConstellation = BossTilePatterns.IsConstellation(pattern) &&
            attack is BossTilePatternAttack { SequentialConstellation: true };
        component.RevealTime = sequentialConstellation ? component.EndTime :
            bar.StartTime + TimeSpan.FromSeconds(BossTilePatterns.IsConstellation(pattern)
                ? 2 : Math.Min(1, duration.TotalSeconds / 4));
        component.DangerRevealTime = (BossTilePatterns.IsConstellation(pattern) ? component.SwitchTime : component.EndTime)
            - TimeSpan.FromSeconds(0.1);
        bar.Visible = bar.Casting && !BossTilePatterns.IsBlackout(pattern);
        Dirty(boss, bar);
        var preparation = new Preparation
        { Root = root, Targets = targets, SequentialConstellation = sequentialConstellation };
        _prepared[boss] = preparation;
        if (showTargetMarkers && pattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack)
            foreach (var target in targets)
                preparation.Marks[target] = CreateMarker(boss, target, pattern == BossTelegraphPattern.Spread ? BossActorMarkerKind.Spread : BossActorMarkerKind.Stack);
        Refresh(component, preparation);
        RefreshEmitters(component, preparation);
        Dirty(root, component);
        return true;
    }

    public bool PrepareMechanic(EntityUid boss, BossAttack attack, IReadOnlyList<EntityUid> targets,
        TimeSpan? start = null, TimeSpan? end = null, bool preload = false, bool storedStrike = false,
        bool showTargetMarkers = true, BossTelegraphPattern? simultaneousPattern = null)
    {
        if (!Prepare(boss, attack, targets, showTargetMarkers)) return false;
        _prepared[boss].MechanicOwned = true;
        _prepared[boss].SimultaneousPattern = simultaneousPattern;
        var warning = Comp<BossTelegraphComponent>(_prepared[boss].Root);
        warning.StartTime = start ?? warning.StartTime;
        warning.AnimationStartTime = warning.StartTime;
        warning.EndTime = end ?? warning.EndTime;
        warning.Preload = preload;
        warning.StoredStrike = storedStrike;
        warning.DangerRevealTime = preload ? warning.StartTime : warning.EndTime - TimeSpan.FromSeconds(
            storedStrike ? Math.Min(2, (warning.EndTime - warning.StartTime).TotalSeconds) : 0.1);
        Refresh(warning, _prepared[boss]);
        Dirty(_prepared[boss].Root, warning);
        return true;
    }

    public void SetMechanicPhase(EntityUid boss, TimeSpan start, TimeSpan end)
    {
        if (GetTelegraph(boss) is not { } warning) return;
        warning.Comp.SecondPhase = true;
        warning.Comp.ShowSecondColor = true;
        warning.Comp.StartTime = start;
        warning.Comp.EndTime = end;
        warning.Comp.DangerRevealTime = end - TimeSpan.FromSeconds(0.1);
        RefreshEmitters(warning.Comp, _prepared[boss]);
        SnapshotDanger(boss);
        Dirty(warning);
    }

    public bool PrepareTraversal(EntityUid boss, BossTelegraphPattern pattern, IReadOnlyList<EntityUid> targets,
        TimeSpan start, TimeSpan end, Vector2i origin, int facing, List<Vector2i> path, bool spread,
        ProtoId<BossTelegraphStylePrototype>? style = null)
    {
        if (!PrepareMechanic(boss, new BossTilePatternAttack { Telegraph = pattern }, targets, start, end,
                simultaneousPattern: spread ? BossTelegraphPattern.Spread : null)) return false;
        var preparation = _prepared[boss];
        preparation.TraversalOwned = true;
        var warning = Comp<BossTelegraphComponent>(preparation.Root);
        if (style is { } selected) warning.Style = selected;
        warning.MovementCleave = true;
        if (spread)
            foreach (var target in targets)
                preparation.Marks[target] = CreateMarker(boss, target, BossActorMarkerKind.Spread);
        SetTraversalFootprint(boss, origin, facing, path);
        return true;
    }

    public void SetTraversalFootprint(EntityUid boss, Vector2i origin, int facing, List<Vector2i> path)
    {
        if (GetTelegraph(boss) is not { } warning) return;
        warning.Comp.PatternOrigin = origin;
        warning.Comp.Facing = facing;
        warning.Comp.MovementPath = path;
        warning.Comp.MovementDestination = path.Count > 0 ? path[^1] : null;
        Refresh(warning.Comp, _prepared[boss]);
        Dirty(warning);
    }

    public void SetSimultaneousPattern(EntityUid boss, BossTelegraphPattern pattern, Vector2i origin = default, int? facing = null)
    {
        if (GetTelegraph(boss) is not { } warning) return;
        var preparation = _prepared[boss];
        preparation.SimultaneousPattern = pattern;
        preparation.SimultaneousOrigin = origin;
        preparation.SimultaneousFacing = facing;
        Refresh(warning.Comp, preparation);
        Dirty(warning);
    }

    public EntityUid CreateIndependentWarning(EntityUid boss, BossTelegraphPattern pattern, Vector2i origin, TimeSpan end)
    {
        var settings = Comp<BossTelegraphSettingsComponent>(boss);
        TryGetArena(boss, out var arena);
        var root = Spawn(settings.TelegraphPrototype, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
        var warning = Comp<BossTelegraphComponent>(root);
        warning.Boss = boss;
        warning.Grid = arena.Comp.CombatGrid;
        warning.HalfSize = arena.Comp.HalfSize;
        warning.Style = EntityManager.System<BossMechanicDefinitionSystem>().Style(boss, pattern) ?? settings.Style;
        warning.Pattern = pattern;
        warning.PatternOrigin = origin;
        warning.StartTime = _timing.CurTime;
        warning.EndTime = end;
        warning.Danger = BossTilePatterns.Danger(pattern, [], origin: origin, halfSize: warning.HalfSize);
        Dirty(root, warning);
        return root;
    }

    public EntityUid? ShowImpact(EntityUid boss, IEnumerable<Vector2i> danger, string? prototype = null, bool additionalSlash = false,
        EntityUid? warningEntity = null, bool playSound = true)
    {
        Entity<BossTelegraphComponent>? source = warningEntity is { } uid && TryComp<BossTelegraphComponent>(uid, out var component)
            ? new(uid, component) : GetTelegraph(boss);
        if (source is not { } warning) return null;
        if (!_prepared.TryGetValue(boss, out var preparation) || preparation.Root != warning.Owner ||
            preparation.SimultaneousPattern is not { } secondary)
            return SpawnImpact(boss, warning.Comp, danger, prototype, additionalSlash, playSound);

        var requested = danger.ToHashSet();                                      // keep each impact inside the requested damage tiles
        var primary = ImpactSource(warning.Comp, warning.Comp.Pattern, warning.Comp.Style,
            warning.Comp.PatternOrigin, warning.Comp.Facing);
        var other = ImpactSource(warning.Comp, secondary,
            EntityManager.System<BossMechanicDefinitionSystem>().Style(boss, secondary) ?? warning.Comp.Style,
            preparation.SimultaneousOrigin, preparation.SimultaneousFacing ?? warning.Comp.Facing);
        var root = SpawnImpact(boss, primary, ImpactTiles(primary).Where(requested.Contains), prototype, additionalSlash, playSound);
        SpawnImpact(boss, other, ImpactTiles(other).Where(requested.Contains), prototype, false, false); // one effect per pattern
        return root;
    }

    private static BossTelegraphComponent ImpactSource(BossTelegraphComponent source, BossTelegraphPattern pattern,
        ProtoId<BossTelegraphStylePrototype> style, Vector2i origin, int facing)
        => new()
        {
            Boss = source.Boss, Grid = source.Grid, HalfSize = source.HalfSize,
            Pattern = pattern, Style = style, PatternOrigin = origin, Facing = facing,
            MovementCleave = source.MovementCleave,
            SecondPhase = pattern == source.Pattern && source.SecondPhase,
            SpreadCenters = pattern == BossTelegraphPattern.Spread ? new(source.SpreadCenters) : new(),
            SpreadTargets = pattern == BossTelegraphPattern.Spread ? new(source.SpreadTargets) : new(),
            StackCenter = pattern == BossTelegraphPattern.Stack ? source.StackCenter : null,
            StackTarget = pattern == BossTelegraphPattern.Stack ? source.StackTarget : null,
            AnimationStartTime = source.AnimationStartTime, EndTime = source.EndTime,
        };

    private static IEnumerable<Vector2i> ImpactTiles(BossTelegraphComponent visual)
    {
        var targets = visual.Pattern == BossTelegraphPattern.Stack && visual.StackCenter is { } center
            ? new List<Vector2i> { center } : visual.SpreadCenters;
        return BossTilePatterns.Danger(visual.Pattern, targets, secondPhase: visual.SecondPhase,
            facing: visual.Facing, origin: visual.PatternOrigin, halfSize: visual.HalfSize);
    }

    private EntityUid SpawnImpact(EntityUid boss, BossTelegraphComponent warning, IEnumerable<Vector2i> danger,
        string? prototype, bool additionalSlash, bool playSound)
    {
        var root = Spawn(string.IsNullOrWhiteSpace(prototype) ? Comp<BossTelegraphSettingsComponent>(boss).ImpactPrototype : prototype, new EntityCoordinates(warning.Grid, 0.5f, 0.5f));
        var impact = Comp<BossTelegraphComponent>(root);
        impact.Boss = boss;
        impact.Grid = warning.Grid;
        impact.HalfSize = warning.HalfSize;
        impact.Style = warning.Style;
        impact.Pattern = warning.Pattern;
        impact.PatternOrigin = warning.PatternOrigin;
        impact.Facing = warning.Facing;
        impact.MovementCleave = warning.MovementCleave;
        impact.SecondPhase = warning.SecondPhase;
        impact.Danger = danger.ToList();
        impact.SpreadCenters = new(warning.SpreadCenters);
        impact.SpreadTargets = new(warning.SpreadTargets);
        impact.StackCenter = warning.StackCenter;
        impact.StackTarget = warning.StackTarget;
        impact.AnimationStartTime = warning.AnimationStartTime;
        impact.Impact = true;
        impact.StartTime = _timing.CurTime;
        var style = _prototypes.Index(impact.Style);
        var lightSweep = BossCleaveVisuals.IsCleave(impact.Pattern) && style.CleaveLight;
        var circularSweep = BossCircularSweepVisuals.Texture(style, impact.Pattern, impact.SecondPhase) != null;
        var crystal = BossRainCrystalVisuals.Texture(style, impact.Pattern, impact: true) != null;
        var river = BossRiverVisuals.Texture(style, impact.Pattern, impact.PatternOrigin, impact.HalfSize, impact.Facing) != null;
        var constellation = BossConstellationVisuals.Texture(style, impact.Pattern, impact.HalfSize) != null;
        var communion = BossCommunionVisuals.Enabled(style, impact.Pattern);
        var vigil = BossVigilVisuals.Enabled(style, impact.Pattern);
        if (vigil)
        {
            impact.EndTime = impact.StartTime + TimeSpan.FromSeconds(BossVigilVisuals.RecoverySeconds);
            EnsureComp<TimedDespawnComponent>(root).Lifetime = BossVigilVisuals.RecoverySeconds;
        }
        else if (communion)
        {
            impact.EndTime = impact.StartTime + TimeSpan.FromSeconds(BossCommunionVisuals.DurationSeconds);
            EnsureComp<TimedDespawnComponent>(root).Lifetime = BossCommunionVisuals.DurationSeconds;
        }
        else if (constellation)
        {
            impact.EndTime = impact.StartTime + TimeSpan.FromSeconds(BossConstellationVisuals.DurationSeconds);
            EnsureComp<TimedDespawnComponent>(root).Lifetime = BossConstellationVisuals.DurationSeconds;
        }
        else if (river)
        {
            impact.EndTime = impact.StartTime + TimeSpan.FromSeconds(BossRiverVisuals.DurationSeconds);
            EnsureComp<TimedDespawnComponent>(root).Lifetime = BossRiverVisuals.DurationSeconds;
        }
        else if (crystal)
        {

            impact.StartTime = warning.EndTime <= _timing.CurTime ? warning.EndTime : _timing.CurTime;
            impact.EndTime = impact.StartTime + TimeSpan.FromSeconds(BossRainCrystalVisuals.ShatterSeconds);
            EnsureComp<TimedDespawnComponent>(root).Lifetime =
                (float)Math.Max(0, (impact.EndTime - _timing.CurTime).TotalSeconds);
        }
        else if (lightSweep || circularSweep)
        {
            impact.EndTime = impact.StartTime + TimeSpan.FromSeconds(BossCleaveVisuals.DurationSeconds);
            EnsureComp<TimedDespawnComponent>(root).Lifetime = BossCleaveVisuals.DurationSeconds;
        }
        var soulSpread = style.SpreadSoulTexture != null && impact.SpreadCenters.Count > 0;
        if (soulSpread && (impact.EndTime - impact.StartTime).TotalSeconds < BossSpreadSoulVisuals.DurationSeconds)
        {
            impact.EndTime = impact.StartTime + TimeSpan.FromSeconds(BossSpreadSoulVisuals.DurationSeconds);
            EnsureComp<TimedDespawnComponent>(root).Lifetime = BossSpreadSoulVisuals.DurationSeconds;
        }
        Dirty(root, impact);
        var animatedTiles = lightSweep || circularSweep || crystal || river ? BossTilePatterns.Danger(impact.Pattern, [],
            secondPhase: impact.SecondPhase, facing: impact.Facing,
            origin: impact.PatternOrigin, halfSize: impact.HalfSize).ToHashSet() : new HashSet<Vector2i>();
        if (soulSpread)
            animatedTiles.UnionWith(BossTilePatterns.Danger(BossTelegraphPattern.Spread, impact.SpreadCenters, halfSize: impact.HalfSize));
        if (communion) animatedTiles.UnionWith(impact.Danger);
        if (vigil) animatedTiles.UnionWith(impact.Danger);
        if (!BossTilePatterns.IsConstellation(impact.Pattern) && Comp<BossTelegraphSettingsComponent>(boss).TileImpactPrototype != null)
            foreach (var tile in impact.Danger)
                if (!animatedTiles.Contains(tile))
                    Spawn(Comp<BossTelegraphSettingsComponent>(boss).TileImpactPrototype, new EntityCoordinates(impact.Grid, tile.X + 0.5f, tile.Y + 0.5f));
        if (!playSound) return root;
        if (BossRainCrystalVisuals.IsCrystal(impact.Pattern))
        {
            var settings = Comp<BossTelegraphSettingsComponent>(boss);
            PlayHitSound(boss, settings.CrystalImpactSound);
        }
        else if (impact.Pattern is BossTelegraphPattern.Sinistra or BossTelegraphPattern.Dextra or
            BossTelegraphPattern.Frons or BossTelegraphPattern.Tergum or
            BossTelegraphPattern.Rejection or BossTelegraphPattern.Invitation)
            PlayHitSound(boss, Comp<BossTelegraphSettingsComponent>(boss).SlashSound);
        else
            PlayHitSound(boss, Comp<BossTelegraphSettingsComponent>(boss).MagicImpactSound);
        if (additionalSlash) PlayHitSound(boss, Comp<BossTelegraphSettingsComponent>(boss).SlashSound);
        return root;
    }

    public void ShowCleavePreviewImpact(EntityUid boss)
    {
        if (GetTelegraph(boss) is not { } warning || warning.Comp.Preload ||
            !(BossCleaveVisuals.IsCleave(warning.Comp.Pattern) && _prototypes.Index(warning.Comp.Style).CleaveLight ||
              BossCircularSweepVisuals.Texture(_prototypes.Index(warning.Comp.Style), warning.Comp.Pattern, warning.Comp.SecondPhase) != null ||
              BossConstellationVisuals.Texture(_prototypes.Index(warning.Comp.Style), warning.Comp.Pattern, warning.Comp.HalfSize) != null ||
              BossCommunionVisuals.Enabled(_prototypes.Index(warning.Comp.Style), warning.Comp.Pattern) ||
              HasCrystalAnimation(boss, warning.Comp.Pattern) ||
              BossRiverVisuals.Texture(_prototypes.Index(warning.Comp.Style), warning.Comp.Pattern,
                  warning.Comp.PatternOrigin, warning.Comp.HalfSize, warning.Comp.Facing) != null)) return;
        ShowImpact(boss, warning.Comp.Danger, playSound: false);
    }

    public bool HasCrystalAnimation(EntityUid boss, BossTelegraphPattern pattern)
        => TryComp<BossTelegraphSettingsComponent>(boss, out var settings) &&
           _prototypes.TryIndex(EntityManager.System<BossMechanicDefinitionSystem>().Style(boss, pattern) ?? settings.Style, out var style) &&
           BossRainCrystalVisuals.Texture(style, pattern, impact: false) != null &&
           BossRainCrystalVisuals.Texture(style, pattern, impact: true) != null;

    public void ShowTankStrike(EntityUid boss, EntityUid target, bool tankBuster = false)
    {
        if (!TryGetArena(boss, out var arena)) return;
        var root = Spawn(Comp<BossTelegraphSettingsComponent>(boss).ImpactPrototype, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
        var strike = Comp<BossTelegraphComponent>(root);
        strike.Style = Comp<BossTelegraphSettingsComponent>(boss).Style;
        strike.Boss = boss;
        strike.Grid = arena.Comp.CombatGrid;
        strike.HalfSize = arena.Comp.HalfSize;
        strike.Impact = true;
        strike.TankTarget = target;
        Dirty(root, strike);
        if (tankBuster)
            PlayHitSound(boss, Comp<BossTelegraphSettingsComponent>(boss).TankBusterImpactSound, tankBuster: true);
    }

    public void PlayTankBusterSound(EntityUid boss)
    {
        if (TryComp<BossTelegraphSettingsComponent>(boss, out var settings))
            PlayHitSound(boss, settings.TankBusterImpactSound, tankBuster: true);
    }

    private void PlayHitSound(EntityUid boss, SoundSpecifier? sound, bool tankBuster = false)
    {
        if (!HasComp<BossEncounterComponent>(boss) || !TryGetArena(boss, out var arena) ||
            !TryComp<BossTelegraphSettingsComponent>(boss, out var settings)) return;
        var volume = tankBuster ? settings.TankBusterImpactSoundVolume ?? settings.ImpactSoundVolume : settings.ImpactSoundVolume;
        var parameters = AudioParams.Default.WithVolume(volume);
        var stream = _audio.PlayPvs(sound, arena.Owner, parameters);
        _audio.SetMapAudio(stream);
    }

    public EntityUid CreateMarker(EntityUid boss, EntityUid target, BossActorMarkerKind kind)
    {
        var settings = Comp<BossTelegraphSettingsComponent>(boss);
        var prototype = kind switch
        {
            BossActorMarkerKind.Spread => settings.SpreadMarkerPrototype,
            BossActorMarkerKind.Stack => settings.StackMarkerPrototype,
            BossActorMarkerKind.TankBuster => settings.TankBusterMarkerPrototype,
            _ => settings.TankMarkerPrototype
        };
        var marker = Spawn(prototype, new EntityCoordinates(target, Vector2.Zero));
        var component = Comp<BossActorMarkerComponent>(marker);
        component.Style = EntityManager.System<BossMechanicDefinitionSystem>().Style(boss, kind switch
        {
            BossActorMarkerKind.Spread => BossTelegraphPattern.Spread,
            BossActorMarkerKind.Stack => BossTelegraphPattern.Stack,
            _ => BossTelegraphPattern.TravelOnly
        }) ?? settings.Style;
        component.Boss = boss;
        component.Target = target;
        component.Kind = kind;
        component.StartTime = _timing.CurTime;
        Dirty(marker, component);
        return marker;
    }

    public Entity<BossTelegraphComponent>? GetTelegraph(EntityUid boss)
        => _prepared.TryGetValue(boss, out var preparation) && TryComp<BossTelegraphComponent>(preparation.Root, out var component)
            ? new(preparation.Root, component) : null;

    public Vector2i[] SnapshotDanger(EntityUid boss)
    {
        if (!_prepared.TryGetValue(boss, out var preparation) || !TryComp<BossTelegraphComponent>(preparation.Root, out var component))
            return Array.Empty<Vector2i>();
        if (Refresh(component, preparation))
            Dirty(preparation.Root, component);
        return component.Danger.ToArray();
    }

    private bool Refresh(BossTelegraphComponent component, Preparation preparation)
    {
        var targets = preparation.Targets.Where(target => TryGetPlayerTile(component.Boss, target, out _)).ToList();
        var tiles = targets.Select(target => { TryGetPlayerTile(component.Boss, target, out var tile); return tile; }).ToList();
        var spread = component.Pattern == BossTelegraphPattern.Spread || preparation.SimultaneousPattern == BossTelegraphPattern.Spread;
        var spreadCenters = spread ? tiles : new List<Vector2i>();
        var spreadTargets = spread ? targets : new List<EntityUid>();
        Vector2i? stackCenter = component.Pattern == BossTelegraphPattern.Stack && tiles.Count > 0 ? tiles[0] : null;
        EntityUid? stackTarget = component.Pattern == BossTelegraphPattern.Stack && targets.Count > 0 ? targets[0] : null;
        var danger = BossTilePatterns.Danger(component.Pattern, tiles, component.SecondPhase, component.Facing, component.PatternOrigin, component.HalfSize);
        if (preparation.SimultaneousPattern is { } simultaneous)
            danger = danger.Union(BossTilePatterns.Danger(simultaneous, tiles,
                facing: preparation.SimultaneousFacing ?? component.Facing, origin: preparation.SimultaneousOrigin,
                halfSize: component.HalfSize)).ToList();
        var safe = BossTilePatterns.GetPlatform(component.HalfSize).Where(tile => !danger.Contains(tile)).ToList();
        if (component.Danger.SequenceEqual(danger) && component.Safe.SequenceEqual(safe) &&
            component.SpreadCenters.SequenceEqual(spreadCenters) && component.SpreadTargets.SequenceEqual(spreadTargets) &&
            component.StackCenter == stackCenter && component.StackTarget == stackTarget)
            return false;
        component.Danger = danger;
        component.Safe = safe;                                                  // safe tiles come from the same danger list
        component.SpreadCenters = new(spreadCenters);
        component.SpreadTargets = new(spreadTargets);
        component.StackCenter = stackCenter;
        component.StackTarget = stackTarget;
        return true;
    }

    private void RefreshEmitters(BossTelegraphComponent warning, Preparation preparation)
    {
        if (!BossTilePatterns.IsConstellation(warning.Pattern)) return;
        var firstBlue = warning.Pattern == BossTelegraphPattern.BlueFirst;
        if (warning.SecondPhase && preparation.Emitters.Remove(firstBlue, out var spent))
            foreach (var emitter in spent)
                if (Exists(emitter) && !Terminating(emitter)) Del(emitter);
        foreach (var blue in new[] { firstBlue, !firstBlue })
        {
            if (warning.SecondPhase && blue == firstBlue || blue != firstBlue && !warning.ShowSecondColor ||
                preparation.Emitters.ContainsKey(blue)) continue;
            var emitters = new List<EntityUid>();
            preparation.Emitters[blue] = emitters;
            foreach (var beam in BossTilePatterns.ConstellationBeams(blue, warning.HalfSize))
                foreach (var edge in new[] { -warning.HalfSize - 0.65f, warning.HalfSize + 1.65f })
                {
                    var center = beam.Start + beam.Width / 2f;
                    var settings = Comp<BossTelegraphSettingsComponent>(warning.Boss);
                    var definition = EntityManager.System<BossMechanicDefinitionSystem>().Pattern(warning.Boss, warning.Pattern);
                    var prototype = blue ? definition?.BlueEmitterPrototype ?? settings.BlueEmitterPrototype
                        : definition?.PurpleEmitterPrototype ?? settings.PurpleEmitterPrototype;
                    if (prototype == null) continue;
                    emitters.Add(Spawn(prototype, new EntityCoordinates(warning.Grid, edge, center)));
                    emitters.Add(Spawn(prototype, new EntityCoordinates(warning.Grid, center, edge)));
                }
        }
    }

    public void Clear(EntityUid boss)
    {
        if (!_prepared.Remove(boss, out var preparation))
            return;
        foreach (var marker in preparation.Marks.Values.Concat(preparation.Emitters.Values.SelectMany(group => group)).Append(preparation.Root))
            if (Exists(marker) && !Terminating(marker))
                Del(marker);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.1);
        foreach (var (boss, preparation) in _prepared.ToArray())
        {
            if (!TryComp<BossComponent>(boss, out var encounter) || encounter.Finished ||
                !(preparation.SequenceOwned ? EntityManager.System<BossActionSystem>().IsActive("stored-cleaves", boss) :
                    preparation.TraversalOwned ? EntityManager.System<BossActionSystem>().IsActive("traversal", boss) :
                    preparation.MechanicOwned ? HasComp<BossMechanicComponent>(boss) : HasComp<BossCastingComponent>(boss)) ||
                !TryComp<BossTelegraphComponent>(preparation.Root, out var component))
            {
                Clear(boss);
                continue;
            }
            if (MetaData(boss).EntityPaused)
                continue;
            if (preparation.SequenceOwned || preparation.TraversalOwned)
                continue;
            var changed = false;
            foreach (var target in preparation.Targets.ToArray())
            {
                if (TryGetPlayerTile(boss, target, out _))
                    continue;
                preparation.Targets.Remove(target);
                if (preparation.Marks.Remove(target, out var marker) && Exists(marker))
                    Del(marker);
            }
            if (component.Pattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack && preparation.Targets.Count == 0)
            {
                EntityManager.System<BossSystem>().CancelBossCast(boss);
                continue;
            }
            if (!preparation.MechanicOwned && BossTilePatterns.IsTwoPhase(component.Pattern) && !component.SecondPhase && _timing.CurTime >= component.SwitchTime)
            {
                if (!component.Preload && BossTilePatterns.IsConstellation(component.Pattern) &&
                    TryComp<BossCastingComponent>(boss, out var cast) && !cast.RequireActiveEncounter && cast.Attack.Actions.Count == 0)
                    ShowImpact(boss, component.Danger, playSound: false);
                component.SecondPhase = true;
                component.DangerRevealTime = component.EndTime - TimeSpan.FromSeconds(0.1);
                changed = true;
            }
            if (!component.ShowSecondColor && _timing.CurTime >= component.RevealTime &&
                (!preparation.SequentialConstellation || component.SecondPhase))
            {
                component.ShowSecondColor = true;
                changed = true;
            }
            if (Refresh(component, preparation) || changed)
                Dirty(preparation.Root, component);
            RefreshEmitters(component, preparation);
        }
    }
}
