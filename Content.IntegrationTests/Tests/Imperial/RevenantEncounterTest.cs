using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mind;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed partial class BossEncounterTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task FirstHitStartsGraceThenLocksGateAndIdleRevivesPartyInFrontWithFreshBoss()
    {
        var (server, arena, players, gate) = await Setup();
        var boss = arena.Comp.Boss;
        EntityUid mind = default;
        await server.WaitAssertion(() =>
        {
            var minds = server.System<SharedMindSystem>();
            mind = minds.CreateMind(null).Owner;
            minds.TransferTo(mind, players[0]);
            server.System<BossArenaMapSystem>().RecordEntry(players[0], gate, arena.Comp.Entrance);
            server.System<BossArenaMapSystem>().RecordEntry(players[1], gate, arena.Comp.Entrance);
            var droppedItem = server.EntMan.SpawnEntity("RevenantBossGateKey",
                new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 1.5f));
            server.System<DamageableSystem>().TryChangeDamage(boss, Hit(1), origin: players[0]);
            Assert.That(server.EntMan.GetComponent<BossComponent>(boss).Active, Is.True);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            Assert.That(rotation.FightRunning, Is.True);
            Assert.That(rotation.Party, Has.Count.EqualTo(4));
            var portal = server.EntMan.GetComponent<BossArenaGateComponent>(gate);
            Assert.That(portal.Open, Is.True);
            Assert.That(portal.CombatLocked, Is.True);
            Assert.That(portal.EntryLocked, Is.False);
            Assert.That(server.System<BossArenaGateSystem>().SetOpen(gate, true), Is.False);
            rotation.EntryCloses = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(portal.Open, Is.False);
            Assert.That(portal.EntryLocked, Is.True);
            foreach (var player in players)
                server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead);
            var ghost = server.EntMan.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            minds.Visit(mind, ghost);
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(rotation.IdleDeadline, Is.Not.Null);
            Assert.That(arena.Comp.Boss, Is.EqualTo(boss), "No immediate revival.");
            rotation.IdleDeadline = server.ResolveDependency<IGameTiming>().CurTime;
            rotation.NextResetAttempt = rotation.IdleDeadline.Value;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(arena.Comp.Boss, Is.Not.EqualTo(boss));
            Assert.That(server.EntMan.EntityExists(droppedItem), Is.True, "Wipe resets retain dropped gear for recovery.");
            Assert.That(server.EntMan.GetComponent<TransformComponent>(droppedItem).MapUid, Is.EqualTo(arena.Owner));
            Assert.That(server.EntMan.EntityExists(arena.Comp.Boss), Is.False);
            Assert.That(arena.Comp.Crystal, Is.Not.Null);
            Assert.That(portal.Open, Is.True);
            Assert.That(portal.CombatLocked || portal.EntryLocked, Is.False);
            foreach (var brazier in arena.Comp.Braziers)
            {
                Assert.That(server.EntMan.GetComponent<PointLightComponent>(brazier).Enabled, Is.False);
                Assert.That(server.System<SharedAppearanceSystem>().TryGetData(brazier,
                    BossBrazierVisuals.State, out BossBrazierState state), Is.True);
                Assert.That(state, Is.EqualTo(BossBrazierState.Empty));
            }
            foreach (var player in players)
            {
                Assert.That(server.System<MobStateSystem>().IsAlive(player), Is.True);
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.EqualTo(95));
                var blood = server.EntMan.GetComponent<BloodstreamComponent>(player);
                Assert.That(blood.BloodSolution!.Value.Comp.Solution.Volume, Is.EqualTo(blood.BloodMaxVolume));
                Assert.That(blood.BleedAmount, Is.Zero);
                var xform = server.EntMan.GetComponent<TransformComponent>(player);
                Assert.That(xform.MapUid, Is.EqualTo(server.EntMan.GetComponent<TransformComponent>(gate).MapUid));
                Assert.That(xform.Coordinates.Position.Y, Is.LessThan(0));
                Assert.That(server.EntMan.HasComponent<BossEncounterParticipantComponent>(player), Is.False);
            }
            Assert.That(server.EntMan.GetComponent<MindComponent>(mind).VisitingEntity, Is.Null);
            Assert.That(server.EntMan.GetComponent<MindComponent>(mind).OwnedEntity, Is.EqualTo(players[0]));
        });
    }

    [Test]
    public async Task EveryLivePatternHitsExactlyItsWarningAndTwoPhaseHitsKeepMovementWindow()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), boss), Is.True);
            foreach (var pattern in Enum.GetValues<BossTelegraphPattern>())
            {
                var damage = server.System<DamageableSystem>();
                foreach (var player in players) damage.SetAllDamage(player, server.EntMan.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
                var mechanics = server.System<BossMechanicSystem>();
                Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack { Telegraph = pattern, CastTime = 4,
                    FollowupDelay = 2, Damage = Hit(1), CastName = server.System<BossMechanicSystem>().CastName(boss, pattern) }), Is.True, pattern.ToString());
                if (BossTilePatterns.IsConstellation(pattern))
                    Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).CastName, Is.EqualTo(Loc.GetString("Revenant_Boss_Constellation")),
                        "The cast name must not disclose the laser color order.");
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                var expected = players.Select(player => server.System<BossTelegraphSystem>().TryGetPlayerTile(boss, player, out var tile) && warning.Danger.Contains(tile)).ToArray();
                var context = server.EntMan.GetComponent<BossMechanicComponent>(boss);
                context.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                mechanics.Update(0);
                for (var i = 0; i < players.Length; i++)
                    Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(), Is.EqualTo(expected[i] ? 1 : 0), pattern.ToString());
                if (BossTilePatterns.IsTwoPhase(pattern))
                {
                    Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss), Is.True);
                    Assert.That((context.Deadline - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds, Is.EqualTo(2));
                    Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.SecondPhase, Is.True);
                    if (BossTilePatterns.IsConstellation(pattern))
                    {
                        var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
                        Assert.That(bar.Casting, Is.False);
                        Assert.That(bar.CastName, Is.Empty);
                    }
                    mechanics.Cancel(boss);
                }
                if (BossTilePatterns.IsBlackout(pattern))
                    FinishVigilRecovery(server, boss);
                Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss), Is.False);
                Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            }
        });
    }

    [Test]
    public async Task TutorialThenOneFullRotationThenEscalationRepeatsOnlyAfterFullMechanicCompletion()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), boss), Is.True);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            var manager = server.System<BossEncounterSystem>();
            var initialCount = rotation.Tutorial.Count + rotation.Rotation.Count;
            for (var total = 0; total < initialCount + 2 * rotation.Escalation.Count; total++)
            {
                Assert.That(rotation.Escalating, Is.EqualTo(total >= initialCount), "Escalation starts after the first complete rotation.");
                var repeating = rotation.Repeating;
                var step = rotation.Step;
                var lap = rotation.Lap;
                var definition = BossEncounterSystem.CurrentSteps(rotation)[step];
                rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
                manager.Update(0);
                Assert.That(rotation.Waiting, Is.True, $"Step {total}");
                Assert.That(rotation.Step, Is.EqualTo(step));
                if (definition.TankBuster)
                {
                    server.EntMan.GetComponent<BossCastBarComponent>(boss).EndTime = server.ResolveDependency<IGameTiming>().CurTime;
                    server.System<BossSystem>().Update(0);
                }
                else if (definition.Traversal != null)
                {
                    while (server.EntMan.HasComponent<BossTraversalComponent>(boss))
                        AdvanceTraversal(server, boss);
                }
                else if (definition.Combo)
                {
                    while (server.EntMan.TryGetComponent<BossSequenceComponent>(boss, out var sequence))
                    {
                        sequence.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                        server.System<BossSequenceSystem>().Update(0);
                    }
                }
                else
                {
                    do
                    {
                        var context = server.EntMan.GetComponent<BossMechanicComponent>(boss);
                        if (!definition.RandomCorner && rotation.PendingSteps.Count == 0)
                            Assert.That(context.Attack.Telegraph, Is.EqualTo(repeating && (lap & 1) != 0 && definition.Alternate != null
                                ? definition.Alternate : definition.Pattern));
                        context.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                        server.System<BossMechanicSystem>().Update(0);
                        if (server.EntMan.HasComponent<BossMechanicComponent>(boss))
                        {
                            Assert.That(rotation.Step, Is.EqualTo(step), "The first of two impacts does not advance the step.");
                            context.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                            server.System<BossMechanicSystem>().Update(0);
                        }
                        if (rotation.PendingSteps.Count == 0) break;
                        Assert.That(rotation.Step, Is.EqualTo(step), "A combination advances only after its final attack.");
                        rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
                        manager.Update(0);
                    } while (true);
                }
                Assert.That(rotation.Waiting, Is.False);
                foreach (var player in players)
                    server.System<DamageableSystem>().SetAllDamage(player, server.EntMan.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
            }
            Assert.That(rotation.Repeating, Is.True);
            Assert.That(rotation.Step, Is.Zero);
            Assert.That(rotation.Escalating, Is.True);
            Assert.That(rotation.Lap, Is.EqualTo(2));
            Assert.That(server.EntMan.GetComponent<BossComponent>(boss).Stage, Is.EqualTo(1), "Progression is independent of HP stages.");
        });
    }

    [Test]
    public async Task VigilSwitchesAmbientAndBrazierStatesAndRestoresOnResolutionCancellationAndDefeat()
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var original = server.EntMan.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor;
            Assert.That(original, Is.EqualTo(arena.Comp.AmbientLight));
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            Assert.That(rotation.Tutorial.Single(step => BossTilePatterns.IsBlackout(step.Pattern)).RandomCorner, Is.True);
            Assert.That(rotation.Rotation.Single(step => BossTilePatterns.IsBlackout(step.Pattern)).RandomCorner, Is.True);
            var appearance = server.System<SharedAppearanceSystem>();
            foreach (var brazier in arena.Comp.Braziers)
            {
                Assert.That(server.EntMan.GetComponent<PointLightComponent>(brazier).Enabled, Is.False);
                Assert.That(appearance.TryGetData(brazier, BossBrazierVisuals.State, out BossBrazierState state), Is.True);
                Assert.That(state, Is.EqualTo(BossBrazierState.Empty));
            }
            for (var i = 0; i < 6; i++)
            {
                if (i == 1) server.EntMan.DeleteEntity(arena.Comp.Braziers[0]);
                var white = i % 4;
                Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = (BossTelegraphPattern)((int)BossTelegraphPattern.BlackoutNorthWest + white),
                    CastTime = 6, Damage = Hit(1) }), Is.True);
                Assert.That(arena.Comp.Braziers, Has.Count.EqualTo(4));
                Assert.That(server.EntMan.GetComponent<BossPresentationComponent>(boss).Hidden, Is.True);
                Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Anchor, Is.EqualTo(arena.Comp.CombatGrid));
                Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Visible, Is.False);
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(0.1).Within(0.000001));
                Assert.That(warning.ShowDanger(warning.StartTime), Is.False);
                Assert.That(warning.ShowDanger(warning.EndTime - TimeSpan.FromMilliseconds(101)), Is.False);
                Assert.That(warning.ShowDanger(warning.EndTime - TimeSpan.FromMilliseconds(50)), Is.False);
                Assert.That(warning.ShowDanger(warning.EndTime), Is.False);
                Assert.That(server.EntMan.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor, Is.EqualTo(Color.Black));
                for (var n = 0; n < 4; n++)
                {
                    var brazier = arena.Comp.Braziers[n];
                    var light = server.EntMan.GetComponent<PointLightComponent>(brazier);
                    Assert.That(light.Enabled, Is.True);
                    Assert.That(light.Color, Is.EqualTo(n == white ? Color.White : Color.FromHex("#BB45FF")));
                    Assert.That(light.Energy, Is.EqualTo(3f));
                    Assert.That(light.Radius, Is.EqualTo(4f));
                    Assert.That(appearance.TryGetData(brazier, BossBrazierVisuals.State, out BossBrazierState state), Is.True);
                    Assert.That(state, Is.EqualTo(n == white ? BossBrazierState.White : BossBrazierState.Purple));
                    Assert.That(appearance.TryGetData(brazier, BossBrazierVisuals.Lit, out bool lit) && lit, Is.True);
                    Assert.That(appearance.TryGetData(brazier, BossBrazierVisuals.Safe, out bool safe), Is.True);
                    Assert.That(safe, Is.EqualTo(n == white));
                }
                Assert.That(server.System<BossArenaMapSystem>().TryEnsureArena(gate,
                    server.EntMan.GetComponent<BossArenaGateComponent>(gate)), Is.True);
                Assert.That(server.EntMan.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor, Is.EqualTo(Color.Black),
                    "Reusing the arena cannot restore ambient illumination mid-blackout.");
                if (i < 4)
                {
                    server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                    server.System<BossMechanicSystem>().Update(0);
                    Assert.That(server.EntMan.HasComponent<BossVigilComponent>(boss), Is.True);
                    FinishVigilRecovery(server, boss);
                }
                else if (i == 4) server.System<BossSystem>().CancelBossCast(boss);
                else server.System<BossSystem>().DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2);
                foreach (var brazier in arena.Comp.Braziers)
                {
                    var light = server.EntMan.GetComponent<PointLightComponent>(brazier);
                    Assert.That(light.Enabled, Is.EqualTo(i == 5));
                    Assert.That(light.Energy, Is.EqualTo(i == 5 ? 8f : 3f));
                    Assert.That(light.Radius, Is.EqualTo(i == 5 ? 8f : 4f));
                    if (i == 5) Assert.That(light.Color, Is.EqualTo(Color.FromHex("#FFD45C")));
                    Assert.That(appearance.TryGetData(brazier, BossBrazierVisuals.State, out BossBrazierState state), Is.True);
                    Assert.That(state, Is.EqualTo(i == 5 ? BossBrazierState.Victory : BossBrazierState.Empty));
                    Assert.That(appearance.TryGetData(brazier, BossBrazierVisuals.Safe, out bool safe) && safe, Is.False);
                }
                Assert.That(server.EntMan.GetComponent<BossPresentationComponent>(boss).Hidden, Is.False);
                Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Visible, Is.True);
                Assert.That(server.EntMan.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor, Is.EqualTo(original));
                Assert.That(server.EntMan.GetComponent<TransformComponent>(boss).Coordinates.Position, Is.EqualTo(new System.Numerics.Vector2(0.5f, 0.5f)));
                Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            }
            Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(gate).CombatLocked, Is.False);
            Assert.That(arena.Comp.VictoryExit, Is.Not.Null);
            Assert.That(server.EntMan.AllEntities<BossArenaCenterBlockerComponent>()
                .Count(ent => server.EntMan.GetComponent<TransformComponent>(ent.Owner).GridUid == arena.Comp.CombatGrid), Is.Zero);
        });
        await server.WaitRunTicks(140);
        await server.WaitAssertion(() =>
        {
            foreach (var brazier in arena.Comp.Braziers)
            {
                Assert.That(server.EntMan.GetComponent<PointLightComponent>(brazier).Enabled, Is.True,
                    "Victory lights remain after the boss is removed.");
                Assert.That(server.System<SharedAppearanceSystem>().TryGetData(brazier,
                    BossBrazierVisuals.State, out BossBrazierState state), Is.True);
                Assert.That(state, Is.EqualTo(BossBrazierState.Victory));
            }
            Assert.That(server.System<BossEncounterSystem>().PrepareRetry(arena), Is.False, "Do not clear rewards while bodies remain.");
            foreach (var player in players) server.EntMan.DeleteEntity(player);
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(server.System<BossEncounterSystem>().PrepareRetry(arena), Is.True);
            Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(gate).Open, Is.False,
                "Victory cleanup must close the entrance before preparing a fresh boss.");
            Assert.That(arena.Comp.VictoryExit, Is.Null);
            Assert.That(server.EntMan.AllEntities<BossEncounterRewardComponent>(), Is.Empty);
            Assert.That(server.EntMan.EntityExists(arena.Comp.Boss), Is.False, "Retry begins at the summoning crystal.");
            Assert.That(arena.Comp.Crystal, Is.Not.Null);
            Assert.That(server.EntMan.AllEntities<BossArenaCenterBlockerComponent>()
                .Count(ent => server.EntMan.GetComponent<TransformComponent>(ent.Owner).GridUid == arena.Comp.CombatGrid), Is.EqualTo(1));
            foreach (var brazier in arena.Comp.Braziers)
            {
                var light = server.EntMan.GetComponent<PointLightComponent>(brazier);
                Assert.That(light.Enabled, Is.False);
                Assert.That(light.Energy, Is.EqualTo(3f));
                Assert.That(light.Radius, Is.EqualTo(4f));
                Assert.That(server.System<SharedAppearanceSystem>().TryGetData(brazier,
                    BossBrazierVisuals.State, out BossBrazierState state), Is.True);
                Assert.That(state, Is.EqualTo(BossBrazierState.Empty));
            }
        });
    }

    [Test]
    public async Task RandomDirectionsAllowEveryDirectionButNeverTripleAcrossResolvedQueues()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var observed = new HashSet<BossTelegraphPattern>();
            var lengths = new HashSet<int>();
            var resolved = new List<BossTelegraphPattern>();
            var attack = new BossPreparedSequenceAttack { RandomDirections = true, PreventTripleRepeats = true,
                Damage = Hit(1), LengthWeights = new() { [2] = 30, [3] = 40, [4] = 30 } };
            for (var attempt = 0; attempt < 120; attempt++)
            {
                Assert.That(server.System<BossSequenceSystem>().TryStart(boss, attack), Is.True);
                var queue = server.EntMan.GetComponent<BossSequenceComponent>(boss).Queue.ToArray();
                observed.UnionWith(queue); lengths.Add(queue.Length); resolved.AddRange(queue);
                while (server.EntMan.TryGetComponent<BossSequenceComponent>(boss, out var sequence))
                {
                    sequence.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                    server.System<BossSequenceSystem>().Update(0);
                    foreach (var player in players)
                        server.System<DamageableSystem>().SetAllDamage(player, server.EntMan.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
                }
            }
            Assert.That(observed, Has.Count.EqualTo(4));
            Assert.That(lengths, Is.EquivalentTo(new[] { 2, 3, 4 }));
            Assert.That(Enumerable.Range(2, resolved.Count - 2).Any(i => resolved[i] == resolved[i - 1] && resolved[i] == resolved[i - 2]), Is.False);
        });
    }

    [Test]
    public async Task SentenceExcludesCurrentTankUntilTheyAreTheOnlyLivingPlayer()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var focus = server.System<BossFocusMarkerSystem>();
            var mechanics = server.System<BossMechanicSystem>();
            var telegraphs = server.System<BossTelegraphSystem>();
            foreach (var tank in players)
            {
                Assert.That(focus.SetTank(boss, tank), Is.True);
                foreach (var pattern in new[] { BossTelegraphPattern.Spread, BossTelegraphPattern.Stack })
                {
                    Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                    { Telegraph = pattern, CastTime = 4, Damage = Hit(1) }), Is.True);
                    var targets = server.EntMan.GetComponent<BossMechanicComponent>(boss).Targets;
                    Assert.That(targets, Does.Not.Contain(tank));
                    Assert.That(targets.Count, Is.EqualTo(pattern == BossTelegraphPattern.Spread ? 3 : 1));
                    var marks = server.EntMan.AllEntities<BossActorMarkerComponent>()
                        .Where(mark => mark.Comp.Boss == boss && mark.Comp.Kind != BossActorMarkerKind.Tank);
                    Assert.That(marks.Select(mark => mark.Comp.Target), Is.EquivalentTo(targets));
                    mechanics.Cancel(boss);
                }
            }
            foreach (var player in players.Skip(1)) server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead);

            focus.Update(0);
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[0]));
            foreach (var pattern in new[] { BossTelegraphPattern.Spread, BossTelegraphPattern.Stack })
            {
                Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = pattern, CastTime = 4, Damage = Hit(1) }), Is.True);
                Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).Targets, Is.EqualTo(new[] { players[0] }));
                Assert.That(telegraphs.GetTelegraph(boss), Is.Not.Null);
                mechanics.Cancel(boss);
            }
        });
    }

    [Test]
    public async Task BossLightFadesBeforeRelocationFreezesDuringPauseAndReturnsAfterVigil()
    {
        var (server, arena, players, _) = await Setup();
        var boss = arena.Comp.Boss;
        float pausedEnergy = 0;
        await server.WaitAssertion(() =>
        {
            var light = server.EntMan.GetComponent<PointLightComponent>(boss);
            Assert.That(light.Enabled, Is.True);
            Assert.That(light.Energy, Is.EqualTo(3));
            Assert.That(light.Radius, Is.GreaterThan(8), "Reach the platform corners.");
            Assert.That(light.CastShadows, Is.False);
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
            { Telegraph = BossTelegraphPattern.BlackoutNorthWest, CastTime = 6, Damage = Hit(1) }), Is.True);
        });
        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            pausedEnergy = server.EntMan.GetComponent<PointLightComponent>(boss).Energy;
            Assert.That(pausedEnergy, Is.InRange(0.1f, 2.9f), "Light fades rather than switching off.");
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).BlackoutRelocated, Is.False);
            Assert.That(server.EntMan.GetComponent<TransformComponent>(boss).Coordinates.Position,
                Is.EqualTo(new System.Numerics.Vector2(0.5f, 0.5f)));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy, Is.EqualTo(pausedEnergy).Within(0.15));
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).BlackoutRelocated, Is.False);
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            var light = server.EntMan.GetComponent<PointLightComponent>(boss);
            Assert.That(light.Enabled, Is.False);
            Assert.That(light.Energy, Is.Zero);
            var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
            Assert.That(mechanic.BlackoutRelocated, Is.True);
            mechanic.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(light.Energy, Is.Zero, "Return begins at darkness.");
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).BlackoutRecovery, Is.True);
        });
        await server.WaitRunTicks(20);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy,
            Is.Zero, "Darkness lingers after the hit."));
        await CrystalSeconds(server, .85);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy,
            Is.InRange(0.1f, 2.9f), "Light fades back in."));
        await CrystalSeconds(server, 1.2);
        await server.WaitAssertion(() =>
        {
            var light = server.EntMan.GetComponent<PointLightComponent>(boss);
            Assert.That(light.Enabled, Is.True);
            Assert.That(light.Energy, Is.EqualTo(3));
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
            { Telegraph = BossTelegraphPattern.BlackoutSouthEast, CastTime = 6, Damage = Hit(1) }), Is.True);
        });
        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var presentation = server.EntMan.GetComponent<BossPresentationComponent>(boss);
            var current = BossLightSystem.EnergyAt(presentation, server.ResolveDependency<IGameTiming>().CurTime);
            Assert.That(current, Is.InRange(0.1f, 2.9f));
            server.System<BossSystem>().CancelBossCast(boss);
            Assert.That(presentation.LightFrom, Is.EqualTo(current), "Cancellation reverses from current brightness without a flash.");
            Assert.That(presentation.LightTo, Is.EqualTo(3));
            Assert.That(presentation.Hidden, Is.False);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy, Is.EqualTo(3)));
    }

    [Test]
    public async Task ConstellationBeamIntersectionsHitOncePerColorAndPreviousPocketsBecomeDangerous()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var positions = new[] { new Vector2i(-4, -4), new Vector2i(-4, -2), new Vector2i(-2, -2), new Vector2i(-5, -5) };
            for (var i = 0; i < players.Length; i++)
                server.System<SharedTransformSystem>().SetCoordinates(players[i],
                    new EntityCoordinates(arena.Comp.CombatGrid, positions[i].X + 0.5f, positions[i].Y + 0.5f));
            foreach (var pattern in new[] { BossTelegraphPattern.BlueFirst, BossTelegraphPattern.PurpleFirst })
            {
                foreach (var player in players)
                    server.System<DamageableSystem>().SetAllDamage(player, server.EntMan.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
                var mechanics = server.System<BossMechanicSystem>();
                Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = pattern, CastTime = 4, FollowupDelay = 2, Damage = Hit(1) }), Is.True);
                var context = server.EntMan.GetComponent<BossMechanicComponent>(boss);
                context.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                mechanics.Update(0);
                var first = pattern == BossTelegraphPattern.BlueFirst ? new[] { 1, 1, 0, 0 } : new[] { 0, 1, 1, 1 };
                for (var i = 0; i < players.Length; i++)
                    Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(), Is.EqualTo(first[i]));
                Assert.That((context.Deadline - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds, Is.EqualTo(2));
                context.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                mechanics.Update(0);
                var total = new[] { 1, 2, 1, 1 };
                for (var i = 0; i < players.Length; i++)
                    Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(), Is.EqualTo(total[i]));
            }
        });
    }

    private static DamageSpecifier Hit(int amount) => new() { DamageDict = { ["Slash"] = FixedPoint2.New(amount) } };

    [Test]
    public async Task ConstellationFlamesChargeInColorOrderFlashOnlyBeforeImpactAndCleanUp()
    {
        var (server, arena, players, _) = await Setup();
        var boss = arena.Comp.Boss;
        EntityUid[] Flames(bool blue) => server.EntMan.AllEntities<MetaDataComponent>()
            .Where(entity => entity.Comp.EntityPrototype?.ID == (blue ? "RevenantBossBlueFlameEmitter" : "RevenantBossPurpleFlameEmitter"))
            .Select(entity => entity.Owner).ToArray();
        await server.WaitAssertion(() => server.System<BossSystem>().TryStartBossfight(players.ToList(), boss));
        foreach (var firstBlue in new[] { true, false })
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = firstBlue ? BossTelegraphPattern.BlueFirst : BossTelegraphPattern.PurpleFirst,
                    CastTime = 4, FollowupDelay = 2, Damage = Hit(1) }), Is.True);
                Assert.That(Flames(firstBlue), Has.Length.EqualTo(12));
                Assert.That(Flames(!firstBlue), Is.Empty);
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.ShowDanger(server.ResolveDependency<IGameTiming>().CurTime), Is.False);
            });
            await server.WaitRunTicks(70);
            await server.WaitAssertion(() =>
            {
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.ShowDanger(server.ResolveDependency<IGameTiming>().CurTime), Is.False,
                    "Flames charge visibly while floor beams stay hidden.");
                foreach (var blue in new[] { true, false })
                {
                    Assert.That(Flames(blue), Has.Length.EqualTo(12));
                    var expected = BossTilePatterns.ConstellationBeams(blue).SelectMany(beam => new[] { -5.65f, 6.65f }
                        .SelectMany(edge => new[] { new System.Numerics.Vector2(edge, beam.Start + beam.Width / 2f),
                            new System.Numerics.Vector2(beam.Start + beam.Width / 2f, edge) })).ToArray();
                    Assert.That(Flames(blue).Select(uid => server.EntMan.GetComponent<TransformComponent>(uid).Coordinates.Position),
                        Is.EquivalentTo(expected), "Every flame aligns with a real beam endpoint.");
                    foreach (var flame in Flames(blue))
                    {
                        Assert.That(server.EntMan.HasComponent<SyncSpriteComponent>(flame), Is.True,
                            "Both colors use the same native animation clock, including later-spawned flames.");
                        Assert.That(server.EntMan.HasComponent<PhysicsComponent>(flame), Is.False);
                        Assert.That(server.EntMan.GetComponent<PointLightComponent>(flame).Color,
                            Is.EqualTo(Color.FromHex(blue ? "#4DBDFF" : "#BC66FF")));
                    }
                }
                Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(0.1).Within(0.000001));
                Assert.That(warning.ShowDanger(warning.DangerRevealTime - TimeSpan.FromMilliseconds(1)), Is.False);
                Assert.That(warning.ShowDanger(warning.DangerRevealTime), Is.True);
                var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
                mechanic.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                server.System<BossMechanicSystem>().Update(0);
                Assert.That(Flames(firstBlue), Is.Empty, "Spent flames disappear after their color fires.");
                Assert.That(Flames(!firstBlue), Has.Length.EqualTo(12));
                warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.ShowDanger(server.ResolveDependency<IGameTiming>().CurTime), Is.False);
                Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(0.1).Within(0.000001));
                if (firstBlue)
                {
                    mechanic.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                    server.System<BossMechanicSystem>().Update(0);
                }
                else server.System<BossSystem>().CancelBossCast(boss);
                Assert.That(Flames(true).Concat(Flames(false)), Is.Empty);
            });
        }
    }

    [Test]
    public async Task VictoryRevivesFallenMembersInsideAndPreservesSurvivorsAndReturnOwnership()
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            foreach (var player in players) server.System<BossArenaMapSystem>().RecordEntry(player, gate, arena.Comp.Entrance);
            Assert.That(server.EntMan.AllEntities<BossArenaCenterBlockerComponent>()
                .Count(ent => server.EntMan.GetComponent<TransformComponent>(ent.Owner).GridUid == arena.Comp.CombatGrid), Is.EqualTo(1));

            server.System<MobStateSystem>().ChangeMobState(players[1], MobState.Critical);
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var minds = server.System<SharedMindSystem>();
            var mind = minds.CreateMind(null).Owner;
            minds.TransferTo(mind, players[0]);
            var position = server.EntMan.GetComponent<TransformComponent>(players[0]).Coordinates;
            server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Dead);
            server.System<SharedTransformSystem>().SetCoordinates(players[1], new EntityCoordinates(arena.Owner, 100, 100));
            var ghost = server.EntMan.SpawnEntity(null, position);
            minds.Visit(mind, ghost);
            server.System<DamageableSystem>().TryChangeDamage(players[2], Hit(7), origin: boss);
            server.System<BossSystem>().DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2);
            for (var i = 0; i < 2; i++)
            {
                Assert.That(server.System<MobStateSystem>().IsAlive(players[i]), Is.True);
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(), Is.EqualTo(95));
                Assert.That(server.System<BossTelegraphSystem>().TryGetPlatformTile(boss, players[i], out _), Is.True);
                Assert.That(server.EntMan.GetComponent<BossArenaVisitorComponent>(players[i]).Gate, Is.EqualTo(gate));
                var blood = server.EntMan.GetComponent<BloodstreamComponent>(players[i]);
                Assert.That(blood.BloodSolution!.Value.Comp.Solution.Volume, Is.EqualTo(blood.BloodMaxVolume));
                Assert.That(blood.BleedAmount, Is.Zero);
            }
            Assert.That(server.EntMan.GetComponent<TransformComponent>(players[0]).Coordinates, Is.EqualTo(position));
            Assert.That(server.EntMan.GetComponent<MindComponent>(mind).VisitingEntity, Is.Null);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(), Is.EqualTo(7));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[3]).TotalDamage.Float(), Is.Zero);
            Assert.That(arena.Comp.VictoryExit, Is.Not.Null);
            var returnFlame = server.EntMan.GetComponent<TransformComponent>(arena.Comp.VictoryExit!.Value);
            Assert.That(returnFlame.ParentUid, Is.EqualTo(arena.Comp.CombatGrid));
            Assert.That(returnFlame.Coordinates.Position, Is.EqualTo(new System.Numerics.Vector2(0.5f, 4.5f)));
            Assert.That(server.EntMan.AllEntities<BossArenaCenterBlockerComponent>()
                .Count(ent => server.EntMan.GetComponent<TransformComponent>(ent.Owner).GridUid == arena.Comp.CombatGrid), Is.Zero);
            Assert.That(server.System<BossEncounterSystem>().PrepareRetry(arena), Is.False);
        });
    }

    [Test]
    public async Task InterruptedDoubleLessonRetriesWithoutTripleOrStalledProgression()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            rotation.Tutorial = [new() { Combo = true, Queue = [BossTelegraphPattern.Sinistra, BossTelegraphPattern.Sinistra] }];
            rotation.Damage["cleave"] = Hit(1);
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            for (var i = 0; i < 3; i++)
            {
                server.EntMan.GetComponent<BossSequenceComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                server.System<BossSequenceSystem>().Update(0);
            }
            Assert.That(server.EntMan.GetComponent<BossSequenceComponent>(boss).StrikesResolved, Is.EqualTo(1));
            server.System<BossSystem>().CancelBossCast(boss);
            Assert.That(rotation.Step, Is.Zero);
            Assert.That(rotation.Waiting, Is.False);
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(rotation.Waiting, Is.True);
            var retry = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            Assert.That(retry.Queue, Is.EqualTo(new[] { BossTelegraphPattern.Dextra, BossTelegraphPattern.Dextra }));
            Assert.That(server.System<BossSequenceSystem>().CanFollowHistory(boss, retry.Queue), Is.True);
        });
    }

    [Test]
    public async Task TankPressureRespectsArmorKeepsFirstFocusAndSuspendsDuringAllCasts()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var damage = server.System<DamageableSystem>();
            damage.TryChangeDamage(boss, Hit(1), origin: players[0]);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            var prototype = server.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>("RevenantBoss");
            Assert.That(rotation.Recovery, Is.EqualTo(7));
            Assert.That(rotation.TankInterval, Is.EqualTo(5));
            rotation.TankDamage = ((BossEncounterComponent)prototype.Components["BossEncounter"].Component).TankDamage;
            Assert.That(rotation.TankDamage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(18)));
            rotation.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            ResolveAutoContact(server, boss);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(18));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.Zero);
            Assert.That(server.EntMan.AllEntities<BossAutoProjectileComponent>().Count(effect => effect.Comp.Boss == boss && effect.Comp.Resolved), Is.EqualTo(1));
            damage.SetAllDamage(players[0], server.EntMan.GetComponent<DamageableComponent>(players[0]), FixedPoint2.Zero);
            var armor = server.EntMan.SpawnEntity("MedievalClothingOuterArmorIron", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            Assert.That(server.System<InventorySystem>().TryEquip(players[0], armor, "outerClothing", force: true), Is.True);
            damage.TryChangeDamage(boss, Hit(100), origin: players[1]);
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[0]));
            rotation.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            ResolveAutoContact(server, boss);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.InRange(0.01, 17.99));
            server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Dead);
            server.System<BossFocusMarkerSystem>().Update(0);
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[1]));
            rotation.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            ResolveAutoContact(server, boss);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.EqualTo(18));
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
            { Telegraph = BossTelegraphPattern.Cross, CastTime = 3, Damage = Hit(1) }), Is.True);
            rotation.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.EqualTo(18));
            Assert.That((rotation.NextTank - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds, Is.EqualTo(5));
            server.System<BossMechanicSystem>().Cancel(boss);
            var combo = (BossPreparedSequenceAttack)server.EntMan.GetComponent<BossComponent>(boss).Stages[1].Attacks.Single();
            Assert.That(server.System<BossSequenceSystem>().TryStart(boss, combo), Is.True);
            rotation.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            server.System<BossSequenceSystem>().Cancel(boss);
            server.EntMan.AddComponent<BossCastingComponent>(boss).Attack = combo;
            rotation.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            server.EntMan.RemoveComponent<BossCastingComponent>(boss);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.EqualTo(18));
            Assert.That(server.EntMan.AllEntities<BossAutoProjectileComponent>().Count(effect => effect.Comp.Boss == boss), Is.EqualTo(3), "Casts never create a tank-strike effect.");
        });
    }

    [Test]
    public async Task BrazierAppearanceKeysRoundTripThroughTheGameStateSerializer()
    {
        var (server, _, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var serializer = server.ResolveDependency<IRobustSerializer>();
            foreach (var brazierState in Enum.GetValues<BossBrazierState>())
            {
                var lit = brazierState != BossBrazierState.Empty;
                var safe = brazierState == BossBrazierState.White;
                object state = new AppearanceComponentState(new Dictionary<Enum, object>
                {
                    [BossBrazierVisuals.Lit] = lit,
                    [BossBrazierVisuals.Safe] = safe,
                    [BossBrazierVisuals.State] = brazierState
                });
                using var stream = new MemoryStream();
                serializer.SerializeDirect(stream, state);
                stream.Position = 0;
                serializer.DeserializeDirect<object>(stream, out var received);
                Assert.That(received, Is.TypeOf<AppearanceComponentState>());
                var appearance = (AppearanceComponentState)received;
                Assert.That(appearance.Data[BossBrazierVisuals.Lit], Is.EqualTo(lit));
                Assert.That(appearance.Data[BossBrazierVisuals.Safe], Is.EqualTo(safe));
                Assert.That(appearance.Data[BossBrazierVisuals.State], Is.EqualTo(brazierState));
            }
        });
    }

    [Test]
    public async Task LostCommunionTargetCancelsAndRetriesWithoutAdvancingThenMapDeletionClearsOwnership()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            rotation.Tutorial = [new() { Pattern = BossTelegraphPattern.Stack, Damage = "sentence" }];
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            var marked = server.EntMan.GetComponent<BossMechanicComponent>(boss).Targets.Single();
            server.System<SharedTransformSystem>().SetCoordinates(marked, new EntityCoordinates(arena.Owner, 100, 100));
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss), Is.False);
            Assert.That(rotation.Step, Is.Zero);
            Assert.That(rotation.Repeating || rotation.Waiting, Is.False);
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).Targets.Single(), Is.Not.EqualTo(marked));
            server.EntMan.DeleteEntity(arena.Owner);
            Assert.That(server.EntMan.AllEntities<BossMechanicComponent>(), Is.Empty);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>(), Is.Empty);
            Assert.That(server.EntMan.AllEntities<BossEncounterParticipantComponent>(), Is.Empty);
        });
    }

    [TestCase(BossTelegraphPattern.Rejection)]
    [TestCase(BossTelegraphPattern.Invitation)]
    public async Task EmbraceCastsOnceAndPauseAndLateUpdatePreserveBothHits(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        var boss = arena.Comp.Boss;
        await server.WaitAssertion(() =>
        {
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
            { Telegraph = pattern, CastTime = 4, FollowupDelay = 2, Damage = Hit(1) }), Is.True);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Casting, Is.True);
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
            Assert.That(mechanic.SecondPhase, Is.False);
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That((mechanic.Deadline - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds, Is.EqualTo(4).Within(0.1));

            server.System<SharedTransformSystem>().SetCoordinates(players[3], new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 2.5f));
            mechanic.Deadline = server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(30);
            server.System<BossMechanicSystem>().Update(30);
            Assert.That(mechanic.SecondPhase, Is.True);
            Assert.That((mechanic.Deadline - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds, Is.EqualTo(2));
            var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
            Assert.That(bar.Casting || bar.Visible, Is.False, "The follow-up must not display a second cast or name.");
            Assert.That(bar.CastName, Is.Empty);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[3]).TotalDamage.Float(), Is.EqualTo(1));
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[3]).TotalDamage.Float(), Is.EqualTo(1), "The follow-up retains its repositioning delay.");
            mechanic.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[3]).TotalDamage.Float(), Is.EqualTo(2));
            Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss), Is.False);
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
        });
    }
    private async Task<(ServerIntegrationInstance Server, Entity<BossArenaMapComponent> Arena, EntityUid[] Players, EntityUid Gate)> Setup(bool crystalOnly = false)
    {
        var options = new ServerIntegrationOptions { Pool = false, ContentStart = true, LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false } };
        foreach (var (cvar, value) in PoolManager.TestCvars) options.CVarOverrides[cvar] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        Entity<BossArenaMapComponent> arena = default;
        var players = new EntityUid[4];
        EntityUid gate = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"),
                out var map, out _, new DeserializationOptions { InitializeMaps = true }), Is.True);
            arena = new(map!.Value.Owner, server.EntMan.GetComponent<BossArenaMapComponent>(map.Value.Owner));
            var maps = server.System<SharedMapSystem>();
            maps.SetPaused(map.Value.Comp.MapId, false);
            var outside = maps.CreateMap(out var outsideId, runMapInit: true);
            var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(outsideId);
            for (var x = -4; x <= 4; x++) for (var y = -4; y <= 4; y++) maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
            gate = server.EntMan.SpawnEntity("RevenantBossGate", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            Assert.That(server.System<BossArenaGateSystem>().LinkGate(gate, arena.Comp.Entrance), Is.True);
            Assert.That(server.System<BossArenaGateSystem>().SetOpen(gate, true), Is.True);
            for (var i = 0; i < players.Length; i++)
            {
                players[i] = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, i * 2 - 2.5f, i % 2 * 4 - 1.5f));
                server.EntMan.RemoveComponent<ShieldOnStartupComponent>(players[i]);
                server.EntMan.RemoveComponent<PassiveDamageComponent>(players[i]);
                server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(players[i]);
            }
            if (!crystalOnly)
            {
                server.EntMan.DeleteEntity(arena.Comp.Crystal!.Value);
                arena.Comp.Crystal = null;
                arena.Comp.Boss = server.EntMan.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
                var encounter = server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss);
                encounter.AutoStart = true;
                encounter.TankDamage = new DamageSpecifier();
                encounter.Damage["buster"] = Hit(1);
                server.System<BossFocusMarkerSystem>().SetTank(arena.Comp.Boss, players[0]);
            }
        });
        return (server, arena, players, gate);
    }
}
