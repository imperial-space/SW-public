using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem : EntitySystem
{
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly SharedBossArenaRulesSystem _rules = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedBloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedDoAfterSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossEncounterComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<BossEncounterComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
    }

    private void OnDamage(EntityUid uid, BossEncounterComponent comp, DamageChangedEvent args)
    {
        if (!comp.AutoStart || comp.FightRunning || !args.DamageIncreased || _rules.ResolveSource(args.Origin) == null ||
            !TryComp<BossComponent>(uid, out var boss) || boss.Finished || boss.Health <= 0 || !_telegraphs.TryGetArena(uid, out _))
            return;
        EntityManager.System<BossSystem>().StartBossfight(_telegraphs.LivingPlayers(uid), uid);
    }

    public void Begin(EntityUid boss)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !_telegraphs.TryGetArena(boss, out var arena))
            return;
        comp.Generation++;
        comp.FightRunning = true;
        comp.Repeating = false;
        comp.Escalating = false;
        comp.PhaseIndex = 0;
        ClearPending(comp);
        comp.LastMixedStackFirst = null;
        comp.Waiting = false;
        comp.Step = comp.Lap = 0;
        comp.IdleDeadline = null;
        comp.RainPressureStart = null;
        comp.Party.Clear();
        comp.Party.UnionWith(Comp<BossComponent>(boss).Players);

        comp.Party.UnionWith(EntityManager.AllEntities<MobStateComponent>()
            .Where(player => _rules.IsPartyBody(player.Owner) && Transform(player.Owner).MapUid == arena.Owner)
            .Select(player => player.Owner));
        comp.EntryCloses = _timing.CurTime + TimeSpan.FromSeconds(comp.EntryGrace);
        comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.OpeningDelay);
        comp.NextTank = _timing.CurTime + TimeSpan.FromSeconds(comp.TankInterval);
        SetGateLocks(boss, true, false);
    }

    public void Join(EntityUid boss, EntityUid player)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !comp.FightRunning || comp.IdleDeadline != null ||
            _timing.CurTime >= comp.EntryCloses || !_telegraphs.TryGetPlayerTile(boss, player, out _))
            return;
        comp.Party.Add(player);
        var encounter = Comp<BossComponent>(boss);
        if (!encounter.Players.Contains(player)) encounter.Players.Add(player);
        var fighting = EnsureComp<BossEncounterParticipantComponent>(player);
        fighting.Boss = GetNetEntity(boss);
        Dirty(player, fighting);
    }

    private void SetGateLocks(EntityUid boss, bool combat, bool entry)
    {
        if (!_telegraphs.TryGetArena(boss, out var arena)) return;
        var gates = EntityQueryEnumerator<BossArenaGateComponent>();
        while (gates.MoveNext(out var gate, out var comp))
        {
            if (comp.ReturnToGate || comp.Destination != arena.Comp.Entrance) continue;
            var changed = comp.EntryLocked != entry;
            comp.CombatLocked = combat;
            comp.EntryLocked = entry;
            if (changed) comp.Revision++;
            if (entry) EntityManager.System<BossArenaGateSystem>().SetOpen(gate, false);
        }
    }

    public void AttachGate(EntityUid boss, EntityUid gate)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !comp.FightRunning ||
            !TryComp<BossArenaGateComponent>(gate, out var portal)) return;
        portal.CombatLocked = true;
        portal.EntryLocked = comp.IdleDeadline != null || _timing.CurTime >= comp.EntryCloses;
        portal.Revision++;
        if (portal.EntryLocked) EntityManager.System<BossArenaGateSystem>().SetOpen(gate, false);
    }

    public void End(EntityUid boss)
    {
        EntityManager.System<BossMusicSystem>().StopFight(boss);
        if (!TryComp<BossEncounterComponent>(boss, out var comp)) return;
        comp.FightRunning = false;
        EntityManager.System<BossAutoProjectileSystem>().CancelAll(boss);
        comp.Waiting = false;
        comp.IdleDeadline = null;
        ClearPending(comp);
        comp.Generation++;
        SetGateLocks(boss, false, false);
    }

    private void OnTerminating(EntityUid uid, BossEncounterComponent comp, ref EntityTerminatingEvent args)
    {
        End(uid);
    }

    private void OnDefeated(ref BossDefeatedEvent args)
    {
        if (!TryComp<BossEncounterComponent>(args.Boss, out var comp) || !_telegraphs.TryGetArena(args.Boss, out var arena)) return;
        arena.Comp.VictoryDeadline = _timing.CurTime + arena.Comp.VictoryLootDuration;
        arena.Comp.NextVictoryReturn = _timing.CurTime;
        arena.Comp.VictoryWarningSent = false;
        if (arena.Comp.VictoryExit is { } exit && TryComp<BossArenaExitTimerComponent>(exit, out var timer))
        {
            timer.EndTime = arena.Comp.VictoryDeadline.Value;
            Dirty(exit, timer);
        }
        EntityManager.System<BossMechanicSystem>().SetBraziersState(arena, BossBrazierState.Victory);
        var gates = EntityManager.System<BossArenaGateSystem>();
        foreach (var player in comp.Party.Where(player => Exists(player) && !Terminating(player) &&
                     Transform(player).MapUid == arena.Owner && !_mobs.IsAlive(player)).ToArray())
        {
            if (!_telegraphs.TryGetPlatformTile(args.Boss, player, out _))
            {
                if (!gates.TryGetPlatformRecoveryPosition(player, arena.Comp.CombatGrid, out var landing)) continue;
                gates.TransportUser(player, landing);
            }
            Revive(player);
        }
        foreach (var player in VictoryOccupants(arena))
            _popup.PopupEntity(Loc.GetString(arena.Comp.MessagePrefix + "Victory_Loot_Time",
                ("minutes", Math.Ceiling(arena.Comp.VictoryLootDuration.TotalMinutes))), player, player, PopupType.Medium);
    }

    public void Revive(EntityUid player)
    {
        EntityManager.System<BossArenaMapSystem>().CancelFall(player);
        if (TryComp<DoAfterComponent>(player, out var actions))
            foreach (var id in actions.DoAfters.Keys.ToArray()) _actions.Cancel(player, id, actions);
        _rejuvenate.PerformRejuvenate(player);
        if (_thresholds.TryGetIncapThreshold(player, out var threshold) && TryComp<DamageableComponent>(player, out var damageable))
        {
            var injury = new DamageSpecifier(damageable.Damage);
            injury.DamageDict["Blunt"] = FixedPoint2.Max(FixedPoint2.Zero, threshold.Value - FixedPoint2.New(BossArenaMapSystem.RecoveryHealth));
            _damage.SetDamage(player, damageable, injury);
        }
        if (TryComp<BloodstreamComponent>(player, out var blood))
        {
            _blood.TryModifyBloodLevel((player, blood), blood.BloodMaxVolume);
            _blood.TryModifyBleedAmount((player, blood), -blood.BleedAmount);
        }
        _mobs.ChangeMobState(player, MobState.Alive);
        if (_minds.TryGetMind(player, out var mind, out var mindComp))
        {
            _minds.UnVisit(mind, mindComp);
            _minds.TransferTo(mind, player, mind: mindComp);
        }
    }

    public void StepFinished(EntityUid boss, bool completed)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !comp.FightRunning || !comp.Waiting || comp.Resetting)
            return;
        comp.Waiting = false;
        comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.Recovery);
        if (!completed)
        {
            ClearPending(comp);
            return;
        }
        if (comp.PendingSteps.Count > 0)
        {
            var finished = comp.PendingSteps[comp.PendingIndex];
            if (finished.StoredStrike && finished.Pattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack)
                ClearStoredTargets(comp, finished.Pattern);
            if (finished.SimultaneousPattern is { } simultaneous)
                ClearStoredTargets(comp, simultaneous);
            if (++comp.PendingIndex < comp.PendingSteps.Count)
            {
                comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(finished.RecoveryAfter ?? comp.Recovery);
                return;
            }
            if (comp.PendingMixedStackFirst is { } stackFirst) comp.LastMixedStackFirst = stackFirst;
            ClearPending(comp);
        }
        comp.Step++;
        var list = CurrentSteps(comp);
        if (comp.Step < list.Count) return;
        comp.Step = 0;
        if (comp.Phases.Count > 0)
        {
            if (comp.Phases[comp.PhaseIndex].Repeat) comp.Lap++;
            else { comp.PhaseIndex++; comp.Lap = 0; }
            return;
        }
        if (comp.Repeating)
        {
            if (!comp.Escalating && comp.Escalation.Count > 0)
            {
                comp.Escalating = true;
                comp.Lap = 0;
            }
            else comp.Lap++;
        }
        else comp.Repeating = true;
    }

    private bool StartStep(EntityUid boss, BossEncounterComponent comp)
    {
        var list = CurrentSteps(comp);
        if (list.Count == 0 || comp.Step >= list.Count) return false;
        var step = list[comp.Step];
        if (comp.PendingSteps.Count == 0 && (step.MixedSentence || step.CrossSentence || step.VigilFollowup != null))
        {
            comp.PendingSteps = BuildCombination(boss, comp, step);
            comp.PendingIndex = 0;
            if (comp.PendingSteps.Count == 0) return false;
        }
        if (comp.PendingSteps.Count > 0) step = comp.PendingSteps[comp.PendingIndex];
        var resolvedPattern = (comp.Phases.Count > 0 || comp.Repeating) && (comp.Lap & 1) != 0 &&
            step.Alternate is { } alternatePattern ? alternatePattern : step.Pattern;
        var damagePattern = step.Traversal == BossTraversalKind.EdgeCone ? BossTelegraphPattern.CardinalCone : resolvedPattern;
        var damageKey = step.TankBuster ? "buster" : comp.PatternDamage.GetValueOrDefault(damagePattern, step.Damage);
        if (!comp.Damage.TryGetValue(damageKey, out var damage)) return false;
        bool started;
        if (step.Traversal is { } traversal)
        {
            comp.Damage.TryGetValue("sentence", out var spreadDamage);
            comp.Damage.TryGetValue("cleave", out var flankDamage);
            started = EntityManager.System<BossTraversalSystem>().TryStart(boss, traversal, damage,
                step.ConeSpread, step.ConeFlank, spreadDamage, step.RainCount,
                rainWarningTime: step.RainWarningTime, rainSizes: step.RainSizes,
                rainCastTime: step.RainCastTime, rainConcurrent: step.RainConcurrent,
                rainSpawnInterval: step.RainSpawnInterval, rainDoubleChance: step.RainDoubleChance,
                rainMaxConcurrent: step.RainMaxConcurrent, rainMinSpawnInterval: step.RainMinSpawnInterval,
                rainRampSeconds: step.RainRampSeconds, flankDamage: step.ConeFlank ? flankDamage : null);
        }
        else if (step.TankBuster)
        {
            if (!TryComp<BossFocusMarkerComponent>(boss, out var focus) || focus.Tank is not { } tank ||
                !_telegraphs.TryGetPlayerTile(boss, tank, out _)) return false;
            started = EntityManager.System<BossSystem>().TryStartBossCast(boss,
                new BossTankBusterAttack
                {
                    CastTime = step.CastTime,
                    CastName = EntityManager.System<BossMechanicSystem>().NameKey(boss, "Execution"),
                    Actions = new() { new BossTankBusterImpact { Damage = damage } }
                }, new[] { tank });
        }
        else if (step.Combo)
        {
            var configured = Comp<BossComponent>(boss).Stages.Values.SelectMany(stage => stage.Attacks)
                .OfType<BossPreparedSequenceAttack>().FirstOrDefault();
            if (configured == null) return false;
            var attack = new BossPreparedSequenceAttack
            {
                Damage = damage,
                PreloadTime = configured.PreloadTime,
                ReleaseTime = configured.ReleaseTime,
                StrikeInterval = configured.StrikeInterval,
                ReleaseName = configured.ReleaseName,
                WarningLeadTime = configured.WarningLeadTime,
                LengthWeights = configured.LengthWeights,
                RandomDirections = true,
                PreventTripleRepeats = true
            };
            var sequences = EntityManager.System<BossSequenceSystem>();
            IReadOnlyList<BossTelegraphPattern>? queue = step.Queue.Count == 0 ? null : step.Queue;

            if (queue != null && !sequences.CanFollowHistory(boss, queue))
                queue = new[] { (0, true), (1, false), (2, false), (3, false) }
                    .Select(change => step.Queue.Select(direction => BossSequenceSystem.TransformDirection(direction, change.Item1, change.Item2)).ToList())
                    .FirstOrDefault(candidate => sequences.CanFollowHistory(boss, candidate));
            started = sequences.TryStart(boss, attack, queue: queue);
        }
        else
        {
            var pattern = resolvedPattern;
            if (step.RandomCorner)
                pattern = (BossTelegraphPattern)((int)BossTelegraphPattern.BlackoutNorthWest + _random.Next(4));
            var name = step.CastName ?? EntityManager.System<BossMechanicSystem>().CastNameKey(boss, pattern);

            if (step.PreloadOnly && step.CastName == null)
                name = EntityManager.System<BossMechanicSystem>().CastNameKey(boss, pattern, sentencing: true);
            var attack = new BossTilePatternAttack
            {
                Telegraph = pattern,
                CastTime = step.CastTime + (step.PreloadOnly || step.StoredStrike ? 2 : 0),
                FollowupDelay = step.FollowupDelay,
                SequentialConstellation = step.SequentialConstellation,
                Damage = damage,
                CastName = name,
                SpreadTargets = step.SpreadTargets
            };
            var targetPattern = step.SimultaneousPattern ?? pattern;
            var remembersTargets = (step.PreloadOnly || step.StoredStrike) &&
                targetPattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack;
            IReadOnlyList<EntityUid>? targets = null;
            if (remembersTargets && step.StoredStrike)
            {
                if (!comp.StoredTargets.TryGetValue(targetPattern, out var stored)) return false;
                targets = stored.Targets;
            }
            BossTilePatternAttack? simultaneousAttack = null;
            if (step.SimultaneousPattern is { } targeted)
            {
                if (!comp.Damage.TryGetValue(comp.PatternDamage.GetValueOrDefault(targeted, "sentence"), out var targetedDamage)) return false;
                simultaneousAttack = new BossTilePatternAttack { Telegraph = targeted, Damage = targetedDamage };
            }
            started = EntityManager.System<BossMechanicSystem>().TryStart(boss, attack,
                step.PreloadOnly, step.StoredStrike, step.HideCast, targets, showTargetMarkers: !remembersTargets,
                simultaneousAttack: simultaneousAttack);
            if (started && remembersTargets && step.PreloadOnly)
                RememberTargets(boss, comp, pattern, Comp<BossMechanicComponent>(boss).Targets);
        }
        comp.Waiting = started;
        return started;
    }

    public override void Update(float frameTime)
    {
        foreach (var arena in EntityManager.AllEntities<BossArenaMapComponent>().ToArray())
        {
            if (arena.Comp.VictoryDeadline is not { } deadline || MetaData(arena.Owner).EntityPaused) continue;
            var occupants = VictoryOccupants(arena);
            if (!arena.Comp.VictoryWarningSent && arena.Comp.VictoryLootDuration > TimeSpan.FromMinutes(1) &&
                deadline - _timing.CurTime <= TimeSpan.FromMinutes(1))
            {
                arena.Comp.VictoryWarningSent = true;
                foreach (var player in occupants)
                    _popup.PopupEntity(Loc.GetString(arena.Comp.MessagePrefix + "Victory_Closing"), player, player, PopupType.MediumCaution);
            }
            if (occupants.Length == 0 || _timing.CurTime >= deadline && _timing.CurTime >= arena.Comp.NextVictoryReturn)
            {
                arena.Comp.NextVictoryReturn = _timing.CurTime + TimeSpan.FromSeconds(1);
                CompleteVictory(arena, occupants);
            }
        }

        foreach (var entity in EntityManager.AllEntities<BossEncounterComponent>().ToArray())
        {
            var boss = entity.Owner;
            var comp = entity.Comp;
            if (!TryComp<BossComponent>(boss, out var encounter)) continue;
            if (!comp.FightRunning || MetaData(boss).EntityPaused) continue;
            if (encounter.Finished || encounter.Health <= 0 || !_telegraphs.TryGetArena(boss, out var arena))
            { End(boss); continue; }
            if (_timing.CurTime >= comp.EntryCloses) SetGateLocks(boss, true, true);
            var living = _telegraphs.LivingPlayers(boss);
            if (living.Count == 0)
            {
                if (comp.IdleDeadline == null)
                {
                    comp.IdleDeadline = _timing.CurTime + TimeSpan.FromSeconds(comp.IdleReset);
                    comp.NextResetAttempt = comp.IdleDeadline.Value;
                    comp.Resetting = true;
                    EntityManager.System<BossSystem>().CancelBossCast(boss);
                    comp.Waiting = false;
                    ClearPending(comp);
                    comp.Resetting = false;
                    SetGateLocks(boss, true, true);
                }
                if (_timing.CurTime >= comp.IdleDeadline && _timing.CurTime >= comp.NextResetAttempt)
                {
                    comp.NextResetAttempt = _timing.CurTime + TimeSpan.FromSeconds(1);
                    Reset(boss, arena);
                }
                continue;
            }
            if (!ValidateStoredTargets(boss, comp)) continue;

            if (comp.IdleDeadline != null)
            {
                comp.IdleDeadline = null;
                comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.Recovery);
                comp.NextTank = _timing.CurTime + TimeSpan.FromSeconds(comp.TankInterval);
            }
            if (_timing.CurTime >= comp.NextTank)
            {
                comp.NextTank = _timing.CurTime + TimeSpan.FromSeconds(comp.TankInterval);
                if (!HasComp<BossCastingComponent>(boss) && !HasComp<BossSequenceComponent>(boss) &&
                    !HasComp<BossMechanicComponent>(boss) &&
                    (!TryComp<BossTraversalComponent>(boss, out var traversal) ||
                        traversal.Kind == BossTraversalKind.CrystalRain && traversal.Phase == BossTraversalPhase.Rain) &&
                    !(TryComp<BossCastBarComponent>(boss, out var bar) && bar.Casting) &&
                    TryComp<BossFocusMarkerComponent>(boss, out var focus) && focus.Tank is { } tank &&
                    _telegraphs.TryGetPlayerTile(boss, tank, out _, allowCritical: true))
                {
                    if (!EntityManager.System<BossAutoProjectileSystem>().TryStart(boss, tank))
                    {
                        _telegraphs.ShowTankStrike(boss, tank);
                        _damage.TryChangeDamage(tank, comp.TankDamage, origin: boss);
                    }
                }
            }
            if (!comp.Waiting && _timing.CurTime >= comp.NextStep &&
                !HasComp<BossCastingComponent>(boss) && !HasComp<BossSequenceComponent>(boss) &&
                !HasComp<BossMechanicComponent>(boss) && !HasComp<BossTraversalComponent>(boss))
            {
                if (!StartStep(boss, comp))
                {
                    ClearPending(comp);
                    comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.Recovery);
                }
            }
        }
    }

    public bool Reset(EntityUid boss, Entity<BossArenaMapComponent> arena)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp)) return false;
        var gates = EntityManager.System<BossArenaGateSystem>();
        var returns = new Dictionary<EntityUid, EntityCoordinates>();
        foreach (var player in comp.Party.Where(player => Exists(player) && !Terminating(player) && Transform(player).MapUid == arena.Owner))
        {
            var gate = TryComp<BossArenaVisitorComponent>(player, out var visitor) ? visitor.Gate : arena.Comp.EntryGate;
            if (gate is not { } exit || !gates.TryGetGateFront(player, exit, out var destination)) return false;
            returns[player] = destination;
        }
        comp.Resetting = true;
        EntityManager.System<BossSystem>().CancelBossCast(boss);
        foreach (var (player, destination) in returns)
        {
            gates.TransportUser(player, destination);
            Revive(player);
            EntityManager.System<BossArenaMapSystem>().LeaveArena(player, arena.Owner);
        }
        End(boss);
        Comp<BossComponent>(boss).Active = false;
        EntityManager.System<BossSystem>().SendPlayersBack(boss, false);
        EntityManager.System<BossFocusMarkerSystem>().Clear(boss);
        Del(boss);
        SpawnFreshBoss(arena);
        Reopen(arena);
        return true;
    }

    public bool PrepareRetry(Entity<BossArenaMapComponent> arena)
        => arena.Comp.VictoryDeadline == null &&
           (arena.Comp.Crystal is { } crystal && Exists(crystal) && !Terminating(crystal) &&
            HasComp<BossSummoningTriggerComponent>(crystal) ||
            Exists(arena.Comp.Boss) && !Terminating(arena.Comp.Boss) &&
            TryComp<BossComponent>(arena.Comp.Boss, out var boss) && !boss.Finished);

    private EntityUid[] VictoryOccupants(Entity<BossArenaMapComponent> arena)
        => EntityManager.AllEntities<MobStateComponent>()
            .Where(player => !Terminating(player.Owner) && _rules.IsPartyBody(player.Owner) && Transform(player.Owner).MapUid == arena.Owner)
            .Select(player => player.Owner).ToArray();

    private void CloseEntryGates(Entity<BossArenaMapComponent> arena)
    {
        var query = EntityQueryEnumerator<BossArenaGateComponent>();
        while (query.MoveNext(out var gate, out var component))
            if (!component.ReturnToGate && component.Destination == arena.Comp.Entrance)
                EntityManager.System<BossArenaGateSystem>().SetOpen(gate, false);
    }

    private void CompleteVictory(Entity<BossArenaMapComponent> arena, EntityUid[] occupants)
    {

        CloseEntryGates(arena);
        var gates = EntityManager.System<BossArenaGateSystem>();
        var returns = new Dictionary<EntityUid, EntityCoordinates>();
        foreach (var player in occupants)
        {
            var entry = TryComp<BossArenaVisitorComponent>(player, out var visitor) ? visitor.Gate : arena.Comp.EntryGate;
            if (entry is { } gate && gates.TryGetGateFront(player, gate, out var destination))
                returns[player] = destination;
            else if (arena.Comp.EntryGate is { } fallback && gates.TryGetGateFront(player, fallback, out destination))
                returns[player] = destination;
            else return;
        }
        foreach (var (player, destination) in returns)
        {
            gates.TransportUser(player, destination);
            if (!_mobs.IsAlive(player)) Revive(player);
            EntityManager.System<BossArenaMapSystem>().LeaveArena(player, arena.Owner);
            _popup.PopupEntity(Loc.GetString(arena.Comp.MessagePrefix + "Victory_Expired"), player, player, PopupType.Medium);
        }
        if (Exists(arena.Comp.Boss) && !Terminating(arena.Comp.Boss)) Del(arena.Comp.Boss);
        ClearVictoryItems(arena);
        SpawnFreshBoss(arena);
    }

    private void ClearVictoryItems(Entity<BossArenaMapComponent> arena)
    {
        var items = EntityManager.AllEntities<ItemComponent>()
            .Where(item => !Terminating(item.Owner) && Transform(item.Owner).MapUid == arena.Owner &&
                           !HasComp<MobStateComponent>(item.Owner))
            .Select(item => item.Owner).ToArray();
        foreach (var item in items)
            if (Exists(item) && !Terminating(item) && Transform(item).MapUid == arena.Owner)
                Del(item);
    }

    private void SpawnFreshBoss(Entity<BossArenaMapComponent> arena)
    {
        EntityManager.System<BossMusicSystem>().StopArena(arena.Owner);
        arena.Comp.VictoryDeadline = null;
        arena.Comp.NextVictoryReturn = TimeSpan.Zero;
        arena.Comp.VictoryWarningSent = false;
        EntityManager.System<BossArenaMapSystem>().SetCenterBlocked(arena, true);
        EntityManager.System<BossMechanicSystem>().SetBraziersState(arena, BossBrazierState.Empty);
        if (arena.Comp.VictoryExit is { } exit && Exists(exit)) Del(exit);
        arena.Comp.VictoryExit = null;
        foreach (var reward in EntityManager.AllEntities<BossEncounterRewardComponent>().Where(ent => Transform(ent.Owner).MapUid == arena.Owner).ToArray())
            Del(reward.Owner);
        EntityManager.System<BossSummoningTriggerSystem>().Restore(arena);
    }

    private void Reopen(Entity<BossArenaMapComponent> arena)
    {
        var query = EntityQueryEnumerator<BossArenaGateComponent>();
        while (query.MoveNext(out var gate, out var component))
            if (!component.ReturnToGate && component.Destination == arena.Comp.Entrance)
                EntityManager.System<BossArenaGateSystem>().SetOpen(gate, true);
    }
}
