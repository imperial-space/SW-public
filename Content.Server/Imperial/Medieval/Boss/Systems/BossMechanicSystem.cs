using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossMechanicSystem : EntitySystem, IBossActionFeature
{
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        EntityManager.System<BossActionSystem>().Register(this);
        SubscribeLocalEvent<BossMechanicComponent, ComponentShutdown>(OnShutdown);
    }

    public string NameKey(EntityUid boss, string suffix)
        => EntityManager.System<BossCastNameSystem>().NameKey(boss, suffix);
    public string CastName(EntityUid boss, BossTelegraphPattern pattern)
        => EntityManager.System<BossCastNameSystem>().CastName(boss, pattern);
    public string CastNameKey(EntityUid boss, BossTelegraphPattern pattern, bool sentencing = false)
        => EntityManager.System<BossCastNameSystem>().CastNameKey(boss, pattern, sentencing);

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
            Terminating(boss) || MetaData(boss).EntityPaused || EntityManager.System<BossActionSystem>().IsBusy(boss) ||
            !_telegraphs.TryGetArena(boss, out var arena)) return false;
        var players = _telegraphs.LivingPlayers(boss);                           // use living players from this arena
        if (players.Count == 0) return false;
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
        mechanic.Generation = TryComp<BossEncounterComponent>(boss, out var rotation) ? rotation.Generation : 0; // remember which fight started this mechanic
        SetPhase(boss, mechanic, attack.CastTime, showCast: !hideCast);
        if (!_telegraphs.PrepareMechanic(boss, attack, targets, mechanic.Start, mechanic.Deadline,
                preloadOnly, storedStrike, showTargetMarkers, simultaneousAttack?.Telegraph)) { Cancel(boss); return false; }
        if (!preloadOnly) Notify(boss, mechanic, BossMechanicPhase.Started);
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

    public bool Cancel(EntityUid boss)
    {
        if (!HasComp<BossMechanicComponent>(boss)) return false;
        RemComp<BossMechanicComponent>(boss);
        return true;
    }

    private void OnShutdown(EntityUid boss, BossMechanicComponent mechanic, ComponentShutdown args)
    {
        _telegraphs.Clear(boss);
        Notify(boss, mechanic, BossMechanicPhase.Stopping);
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

    private bool Notify(EntityUid boss, BossMechanicComponent mechanic, BossMechanicPhase phase,
        EntityUid? impact = null, List<EntityUid>? victims = null)
    {
        var request = new BossMechanicEvent(boss, mechanic, phase, impact, victims);
        RaiseLocalEvent(ref request);
        return request.Handled;
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
            if (Notify(boss, mechanic, BossMechanicPhase.Updating)) continue;
            if (_timing.CurTime < mechanic.Deadline) continue;
            if (mechanic.PreloadOnly)
            {

                mechanic.Completed = true;                                                        // stored warning only, no hit yet
                RemComp<BossMechanicComponent>(boss);
                continue;
            }
            var danger = _telegraphs.SnapshotDanger(boss).ToHashSet();           // freeze damage tiles when the warning ends
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
                if (simultaneousDanger.Contains(tile)) hit += mechanic.SimultaneousAttack!.Damage; // overlapping attacks add both damage amounts
                victims.Add((player.Owner, hit));
            }
            var impact = _telegraphs.ShowImpact(boss, danger, mechanic.Attack.ImpactEffect);
            Notify(boss, mechanic, BossMechanicPhase.Impact, impact, victims.Select(victim => victim.Player).ToList());
            if (mechanic.Attack.Telegraph is { } direction && BossCleaveVisuals.IsCleave(direction))
                EntityManager.System<BossAttackHistorySystem>().RecordStrike(boss, direction);
            foreach (var victim in victims)
            {
                if (!HasComp<BossMechanicComponent>(boss) || !encounter.Active || encounter.Finished) break;
                _damage.TryChangeDamage(victim.Player, victim.Damage, origin: boss);
            }
            if (!HasComp<BossMechanicComponent>(boss)) continue;
            if (Notify(boss, mechanic, BossMechanicPhase.Recovery)) continue;
            if (!mechanic.SecondPhase && mechanic.Attack.Telegraph is { } pattern && BossTilePatterns.IsTwoPhase(pattern))
            {
                mechanic.SecondPhase = true;                                     // start the second hit of this pattern

                SetPhase(boss, mechanic, mechanic.Attack.FollowupDelay,
                    showCast: false);
                _telegraphs.SetMechanicPhase(boss, mechanic.Start, mechanic.Deadline);
            }
            else { mechanic.Completed = true; RemComp<BossMechanicComponent>(boss); }
        }
    }
}
