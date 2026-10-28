using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossMechanicSystem : EntitySystem
{
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly BossLightSystem _presentationLights = default!;

    public override void Initialize()
        => SubscribeLocalEvent<BossMechanicComponent, ComponentShutdown>(OnShutdown);

    public string NameKey(EntityUid boss, string suffix)
        => (TryComp<BossTelegraphSettingsComponent>(boss, out var settings) ? settings.CaptionPrefix : "Boss_Encounter_") + suffix;

    public string CastName(EntityUid boss, BossTelegraphPattern pattern)
        => Robust.Shared.Localization.Loc.GetString(CastNameKey(boss, pattern));

    public string CastNameKey(EntityUid boss, BossTelegraphPattern pattern, bool sentencing = false)
    {
        if (TryComp<BossTelegraphSettingsComponent>(boss, out var settings) &&
            (sentencing ? settings.PreparedCastNames : settings.CastNames).TryGetValue(pattern, out var name)) return name;
        return NameKey(boss, (sentencing ? "Sentencing_" : "") + (pattern switch
        {
            BossTelegraphPattern.Sinistra => "Occidens",
            BossTelegraphPattern.Dextra => "Oriens",
            BossTelegraphPattern.Frons => "Septentrio",
            BossTelegraphPattern.Tergum => "Meridies",
            BossTelegraphPattern.Rejection => "Deaths_Bliss",
            BossTelegraphPattern.Invitation => "Deaths_Blight",
            BossTelegraphPattern.Cross => "Cross",
            BossTelegraphPattern.Divide => "Divide",
            BossTelegraphPattern.Spread => "Severance",
            BossTelegraphPattern.Stack => "Communion",
            BossTelegraphPattern.BlueFirst or BossTelegraphPattern.PurpleFirst => "Constellation",
            _ => "Vigil"
        }));
    }

    public static bool IsValidTimings(float castTime, float followupDelay)
        => float.IsFinite(castTime) && castTime is > 0 and <= 120 &&
           float.IsFinite(followupDelay) && followupDelay is > 0 and <= 120;

    public static bool IsValidDamage(DamageSpecifier damage)
        => !damage.Empty && damage.AnyPositive() && damage.DamageDict.Values.All(value => value >= 0);

    public bool TryStart(EntityUid boss, BossTilePatternAttack attack, bool preloadOnly = false,
        bool storedStrike = false, bool hideCast = false,
        IReadOnlyList<EntityUid>? selectedTargets = null, bool showTargetMarkers = true,
        BossTilePatternAttack? simultaneousAttack = null)
    {
        if (attack.Telegraph is not { } pattern || !IsValidTimings(attack.CastTime, attack.FollowupDelay) ||
            !IsValidDamage(attack.Damage) ||
            !TryComp<BossComponent>(boss, out var encounter) || !encounter.Active || encounter.Finished || encounter.Health <= 0 ||
            Terminating(boss) || MetaData(boss).EntityPaused || HasComp<BossCastingComponent>(boss) ||
            HasComp<BossSequenceComponent>(boss) || HasComp<BossMechanicComponent>(boss) || HasComp<BossTraversalComponent>(boss) ||
            !_telegraphs.TryGetArena(boss, out var arena)) return false;
        var players = _telegraphs.LivingPlayers(boss);
        if (players.Count == 0) return false;
        if (BossTilePatterns.IsBlackout(pattern) && arena.Comp.BrazierPrototype == null) return false;
        if (simultaneousAttack != null &&
            (preloadOnly || !storedStrike || pattern is not (BossTelegraphPattern.Cross or BossTelegraphPattern.Divide) ||
             simultaneousAttack.Telegraph is not (BossTelegraphPattern.Spread or BossTelegraphPattern.Stack) ||
             selectedTargets == null || !IsValidDamage(simultaneousAttack.Damage))) return false;
        var targets = selectedTargets == null ? attack.PickTargets(boss, players, EntityManager).ToList()
            : selectedTargets.Where(target => _telegraphs.TryGetPlayerTile(boss, target, out _)).Distinct().ToList();
        if ((pattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack || simultaneousAttack != null) && targets.Count == 0) return false;
        var mechanic = AddComp<BossMechanicComponent>(boss);
        mechanic.Attack = attack;
        mechanic.SimultaneousAttack = simultaneousAttack;
        mechanic.Arena = arena.Owner;
        mechanic.Grid = arena.Comp.CombatGrid;
        mechanic.Targets = targets;
        mechanic.PreloadOnly = preloadOnly;
        mechanic.Generation = TryComp<BossEncounterComponent>(boss, out var rotation) ? rotation.Generation : 0;
        SetPhase(boss, mechanic, attack.CastTime, showCast: !hideCast);
        if (!_telegraphs.PrepareMechanic(boss, attack, targets, mechanic.Start, mechanic.Deadline,
                preloadOnly, storedStrike, showTargetMarkers, simultaneousAttack?.Telegraph)) { Cancel(boss); return false; }
        if (!preloadOnly && BossTilePatterns.IsBlackout(pattern)) StartBlackout(boss, mechanic, arena, pattern);
        return true;
    }

    private void SetPhase(EntityUid boss, BossMechanicComponent mechanic, float duration, bool showCast = true)
    {
        mechanic.Start = _timing.CurTime;
        mechanic.Deadline = mechanic.Start + TimeSpan.FromSeconds(duration);
        var bar = EnsureComp<BossCastBarComponent>(boss);
        bar.Casting = showCast;
        bar.Preparing = mechanic.PreloadOnly;
        bar.Visible = showCast && !BossTilePatterns.IsBlackout(mechanic.Attack.Telegraph!.Value);
        if (showCast && bar.Visible)
            bar.SetCastName(string.IsNullOrWhiteSpace(mechanic.Attack.CastName)
                ? CastNameKey(boss, mechanic.Attack.Telegraph!.Value) : mechanic.Attack.CastName);
        else
            bar.ClearCastName();
        bar.StartTime = showCast ? mechanic.Start : TimeSpan.Zero;
        bar.EndTime = showCast ? mechanic.Deadline : TimeSpan.Zero;
        bar.Anchor = mechanic.Blackout ? mechanic.Grid : null;
        Dirty(boss, bar);
    }

    public void EnsureBraziers(Entity<BossArenaMapComponent> arena)
    {
        if (!Exists(arena.Comp.CombatGrid) || arena.Comp.BrazierPrototype == null) return;
        arena.Comp.Braziers.RemoveAll(uid => !Exists(uid) || Terminating(uid));
        if (arena.Comp.Braziers.Count == 4) return;
        foreach (var old in arena.Comp.Braziers) Del(old);
        arena.Comp.Braziers.Clear();
        foreach (var pattern in new[] { BossTelegraphPattern.BlackoutNorthWest, BossTelegraphPattern.BlackoutNorthEast,
                     BossTelegraphPattern.BlackoutSouthWest, BossTelegraphPattern.BlackoutSouthEast })
        {
            var tile = BossTilePatterns.BlackoutCenter(pattern, arena.Comp.HalfSize);
            var brazier = Spawn(arena.Comp.BrazierPrototype, new EntityCoordinates(arena.Comp.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
            var animation = EnsureComp<BossBrazierAnimationComponent>(brazier);
            animation.StartTime = _timing.CurTime;
            animation.PhaseOffsetFrames = BossBrazierAnimation.CornerOffset((int)pattern - (int)BossTelegraphPattern.BlackoutNorthWest);
            Dirty(brazier, animation);
            arena.Comp.Braziers.Add(brazier);
            SetBrazierState(brazier, BossBrazierState.Empty);
        }
    }

    public void SetBraziersState(Entity<BossArenaMapComponent> arena, BossBrazierState state)
    {
        EnsureBraziers(arena);
        foreach (var brazier in arena.Comp.Braziers)
            SetBrazierState(brazier, state);
    }

    private void SetBrazierState(EntityUid brazier, BossBrazierState state)
    {
        var lit = state != BossBrazierState.Empty;
        _appearance.SetData(brazier, BossBrazierVisuals.State, state);
        _appearance.SetData(brazier, BossBrazierVisuals.Lit, lit);
        _appearance.SetData(brazier, BossBrazierVisuals.Safe, state == BossBrazierState.White);
        _lights.SetColor(brazier, state switch
        {
            BossBrazierState.White => Color.White,
            BossBrazierState.Victory => Color.FromHex("#FFD45C"),
            _ => Color.FromHex("#BB45FF")
        });
        _lights.SetEnergy(brazier, state == BossBrazierState.Victory ? 8f : 3f);
        _lights.SetRadius(brazier, state == BossBrazierState.Victory ? 8f : 4f);
        _lights.SetEnabled(brazier, lit);
    }

    private void StartBlackout(EntityUid boss, BossMechanicComponent mechanic,
        Entity<BossArenaMapComponent> arena, BossTelegraphPattern pattern)
    {
        EnsureBraziers(arena);
        mechanic.Blackout = true;
        EntityManager.System<BossArenaMapSystem>().SetCenterBlocked(arena, false);
        mechanic.OriginalLight = EnsureComp<MapLightComponent>(arena.Owner).AmbientLightColor;
        _maps.SetAmbientLight(Transform(arena.Owner).MapID, Color.Black);
        var white = (int)pattern - (int)BossTelegraphPattern.BlackoutNorthWest;
        for (var i = 0; i < arena.Comp.Braziers.Count; i++)
        {
            var brazier = arena.Comp.Braziers[i];
            SetBrazierState(brazier, i == white ? BossBrazierState.White : BossBrazierState.Purple);
        }
        var presentation = EnsureComp<BossPresentationComponent>(boss);
        presentation.Hidden = true;
        _presentationLights.Fade(boss, false, presentation);
        mechanic.BlackoutMoveTime = presentation.LightEnd;
        Dirty(boss, presentation);
        if (_telegraphs.GetTelegraph(boss) is { } warning && warning.Comp.SuppressDanger)
        {
            var vigil = EnsureComp<BossVigilComponent>(boss);
            vigil.Arena = arena.Owner;
            vigil.Grid = mechanic.Grid;
            vigil.Style = warning.Comp.Style;
            vigil.OriginalAmbient = mechanic.OriginalLight!.Value;
            vigil.StartTime = mechanic.Start;
            vigil.Resolved = false;
            vigil.Victims.Clear();
            Dirty(boss, vigil);
        }
        var bar = Comp<BossCastBarComponent>(boss);
        bar.Anchor = mechanic.Grid;
        Dirty(boss, bar);
    }

    public bool Cancel(EntityUid boss)
    {
        if (!HasComp<BossMechanicComponent>(boss)) return false;
        RemComp<BossMechanicComponent>(boss);
        return true;
    }

    private void OnShutdown(EntityUid boss, BossMechanicComponent mechanic, ComponentShutdown args)
    {
        _telegraphs.Clear(boss);
        if (mechanic.BlackoutImpact is { } impact && Exists(impact) && !Terminating(impact)) Del(impact);
        RemComp<BossVigilComponent>(boss);
        if (mechanic.Blackout)
        {
            if (Exists(mechanic.Arena) && !Terminating(mechanic.Arena) && TryComp<BossArenaMapComponent>(mechanic.Arena, out var arena))
            {
                if (mechanic.OriginalLight is { } light) _maps.SetAmbientLight(Transform(mechanic.Arena).MapID, light);
                foreach (var brazier in arena.Braziers.Where(uid => Exists(uid) && !Terminating(uid)))
                {
                    SetBrazierState(brazier, BossBrazierState.Empty);
                }
            }
            if (TryComp<BossPresentationComponent>(boss, out var presentation))
            {
                presentation.Hidden = false;
                if (!Terminating(boss) && !(mechanic.Completed && mechanic.BlackoutRecovery))
                    _presentationLights.Fade(boss, true, presentation);
                Dirty(boss, presentation);
            }
            if (!Terminating(boss) && Exists(mechanic.Grid) && !Terminating(mechanic.Grid))
                EntityManager.System<BossArenaProtectionSystem>().MoveBoss(boss, new EntityCoordinates(mechanic.Grid, 0.5f, 0.5f));
            if (!Terminating(boss) && Exists(mechanic.Arena) && !Terminating(mechanic.Arena) &&
                TryComp<BossArenaMapComponent>(mechanic.Arena, out var activeArena) &&
                activeArena.Boss == boss && TryComp<BossComponent>(boss, out var activeBoss) &&
                !activeBoss.Finished && activeBoss.Health > 0)
                EntityManager.System<BossArenaMapSystem>().SetCenterBlocked(new(mechanic.Arena, activeArena), true);
        }
        if (Terminating(boss)) return;
        if (TryComp<BossCastBarComponent>(boss, out var bar))
        {
            bar.Casting = false; bar.Anchor = null; bar.ClearCastName();
            bar.Visible = true;
            bar.StartTime = bar.EndTime = TimeSpan.Zero;
            Dirty(boss, bar);
        }
        if (!TryComp<BossEncounterComponent>(boss, out var encounter) || encounter.Generation == mechanic.Generation)
            EntityManager.System<BossEncounterSystem>().StepFinished(boss, mechanic.Completed);
    }

    private void UpdateBlackoutRecovery(EntityUid boss, BossMechanicComponent mechanic)
    {
        if (!TryComp<BossVigilComponent>(boss, out var vigil)) { Cancel(boss); return; }
        var returnTime = vigil.ResolveTime + TimeSpan.FromSeconds(BossVigilVisuals.HoldSeconds);
        if (!mechanic.BlackoutReturnStarted && _timing.CurTime >= returnTime)
        {
            mechanic.BlackoutReturnStarted = true;
            EntityManager.System<BossArenaProtectionSystem>().MoveBoss(boss, new EntityCoordinates(mechanic.Grid, .5f, .5f));
            if (TryComp<BossPresentationComponent>(boss, out var presentation))
                _presentationLights.Fade(boss, true, presentation, BossVigilVisuals.LightReturnSeconds, returnTime);
        }
        if (BossVigilVisuals.BodyVisible(vigil, _timing.CurTime) &&
            TryComp<BossPresentationComponent>(boss, out var visible) && visible.Hidden)
        {
            visible.Hidden = false;
            Dirty(boss, visible);
        }
        EntityManager.System<BossVigilLightSystem>().Apply(vigil, _timing.CurTime);
        if (_timing.CurTime < mechanic.Deadline) return;
        mechanic.Completed = true;
        RemComp<BossMechanicComponent>(boss);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BossMechanicComponent>();
        while (query.MoveNext(out var boss, out var mechanic))
        {
            if (MetaData(boss).EntityPaused) continue;
            if (!TryComp<BossComponent>(boss, out var encounter) || !encounter.Active || encounter.Finished || encounter.Health <= 0 ||
                !_telegraphs.TryGetArena(boss, out var arena) || arena.Owner != mechanic.Arena || arena.Comp.CombatGrid != mechanic.Grid ||
                _telegraphs.LivingPlayers(boss).Count == 0 || _telegraphs.GetTelegraph(boss) == null ||
                ((mechanic.Attack.Telegraph is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack || mechanic.SimultaneousAttack != null) &&
                 !mechanic.Targets.Any(target => _telegraphs.TryGetPlayerTile(boss, target, out _))))
            { Cancel(boss); continue; }
            if (mechanic.BlackoutRecovery)
            {
                UpdateBlackoutRecovery(boss, mechanic);
                continue;
            }
            if (mechanic.Blackout && !mechanic.BlackoutRelocated && _timing.CurTime >= mechanic.BlackoutMoveTime)
            {

                _lights.SetEnergy(boss, 0);
                _lights.SetEnabled(boss, false);
                var tile = BossTilePatterns.BlackoutCenter(mechanic.Attack.Telegraph!.Value, arena.Comp.HalfSize);
                EntityManager.System<BossArenaProtectionSystem>().MoveBoss(boss,
                    new EntityCoordinates(mechanic.Grid, tile.X + 0.5f, tile.Y + 0.5f + Math.Sign(tile.Y) * 0.8f));
                mechanic.BlackoutRelocated = true;
            }
            if (_timing.CurTime < mechanic.Deadline) continue;
            if (mechanic.PreloadOnly)
            {

                mechanic.Completed = true;
                RemComp<BossMechanicComponent>(boss);
                continue;
            }
            var danger = _telegraphs.SnapshotDanger(boss).ToHashSet();
            var primaryDanger = danger;
            var simultaneousDanger = new HashSet<Vector2i>();
            if (mechanic.SimultaneousAttack is { } simultaneous)
            {
                var facing = _telegraphs.GetTelegraph(boss)!.Value.Comp.Facing;
                var tiles = mechanic.Targets.Where(target => _telegraphs.TryGetPlayerTile(boss, target, out _))
                    .Select(target => { _telegraphs.TryGetPlayerTile(boss, target, out var tile); return tile; }).ToList();
                primaryDanger = BossTilePatterns.Danger(mechanic.Attack.Telegraph!.Value, tiles, facing: facing, halfSize: arena.Comp.HalfSize).ToHashSet();
                simultaneousDanger = BossTilePatterns.Danger(simultaneous.Telegraph!.Value, tiles, facing: facing, halfSize: arena.Comp.HalfSize).ToHashSet();
            }
            var victims = new List<(EntityUid Player, DamageSpecifier Damage)>();
            foreach (var player in EntityManager.AllEntities<MobStateComponent>())
            {
                if (!_telegraphs.TryGetPlayerTile(boss, player.Owner, out var tile, allowCritical: true) || !danger.Contains(tile)) continue;
                var hit = primaryDanger.Contains(tile) ? new DamageSpecifier(mechanic.Attack.Damage) : new DamageSpecifier();
                if (simultaneousDanger.Contains(tile)) hit += mechanic.SimultaneousAttack!.Damage;
                victims.Add((player.Owner, hit));
            }
            var impact = _telegraphs.ShowImpact(boss, danger, mechanic.Attack.ImpactEffect);
            if (mechanic.Blackout && TryComp<BossVigilComponent>(boss, out var resolvedVigil))
            {
                mechanic.BlackoutImpact = impact;
                resolvedVigil.Resolved = true;
                resolvedVigil.ResolveTime = mechanic.Deadline;
                resolvedVigil.Victims = victims.Select(victim => victim.Player).ToList();
                Dirty(boss, resolvedVigil);
            }
            if (mechanic.Attack.Telegraph is { } direction && BossSequenceSystem.IsCleave(direction))
                EntityManager.System<BossSequenceSystem>().RecordStrike(boss, direction);
            foreach (var victim in victims)
            {
                if (!HasComp<BossMechanicComponent>(boss) || !encounter.Active || encounter.Finished) break;
                _damage.TryChangeDamage(victim.Player, victim.Damage, origin: boss);
            }
            if (!HasComp<BossMechanicComponent>(boss)) continue;
            if (mechanic.Blackout && TryComp<BossVigilComponent>(boss, out var recoveringVigil))
            {
                mechanic.BlackoutRecovery = true;
                mechanic.Deadline = recoveringVigil.ResolveTime + TimeSpan.FromSeconds(BossVigilVisuals.RecoverySeconds);
                if (TryComp<BossCastBarComponent>(boss, out var recoveringBar))
                {
                    recoveringBar.Casting = false;
                    Dirty(boss, recoveringBar);
                }
                UpdateBlackoutRecovery(boss, mechanic);
                continue;
            }
            if (!mechanic.SecondPhase && mechanic.Attack.Telegraph is { } pattern && BossTilePatterns.IsTwoPhase(pattern))
            {
                mechanic.SecondPhase = true;

                SetPhase(boss, mechanic, mechanic.Attack.FollowupDelay,
                    showCast: false);
                _telegraphs.SetMechanicPhase(boss, mechanic.Start, mechanic.Deadline);
            }
            else { mechanic.Completed = true; RemComp<BossMechanicComponent>(boss); }
        }
    }
}
