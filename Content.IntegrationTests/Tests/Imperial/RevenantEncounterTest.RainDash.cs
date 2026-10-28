using System.Collections.Generic;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(BossTraversalKind.StepCleave, 0)] [TestCase(BossTraversalKind.StepCleave, 1)]
    [TestCase(BossTraversalKind.StepCleave, 2)] [TestCase(BossTraversalKind.StepCleave, 3)]
    [TestCase(BossTraversalKind.EdgeCone, 0)] [TestCase(BossTraversalKind.EdgeCone, 1)]
    [TestCase(BossTraversalKind.EdgeCone, 2)] [TestCase(BossTraversalKind.EdgeCone, 3)]
    public async Task TraversalUsesOneAcceleratingCurveAcrossEveryTileAndOneSmoothReturn(BossTraversalKind kind, int direction)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var system = server.System<BossTraversalSystem>();
            Assert.That(system.TryStart(boss, kind, Hit(1), direction: direction, distance: 3), Is.True);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var xform = entities.GetComponent<TransformComponent>(boss);
            var timing = server.ResolveDependency<IGameTiming>();
            var axis = BossTraversalSystem.DirectionTile(direction);
            Assert.That(state.Distance, Is.EqualTo(kind == BossTraversalKind.EdgeCone ? 7 : 3));
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>(), Is.Empty);
            AdvanceTraversal(server, boss);
            var duration = kind == BossTraversalKind.EdgeCone ? 0.8 : 0.65;
            Assert.That((state.Deadline - state.DashStart).TotalSeconds, Is.EqualTo(duration).Within(0.0001));
            foreach (var progress in new[] { 0.1, 0.25, 0.5, 0.75, 0.9 })
            {
                state.DashStart = timing.CurTime - TimeSpan.FromSeconds(duration * progress);
                state.Deadline = timing.CurTime + TimeSpan.FromSeconds(duration * (1 - progress));
                system.Update(0);
                var distance = state.Distance * progress * progress * (3 - 2 * progress);
                Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Dashing));
                Assert.That(xform.Coordinates.X, Is.EqualTo(0.5 + axis.X * distance).Within(0.0001));
                Assert.That(xform.Coordinates.Y, Is.EqualTo(0.5 + axis.Y * distance).Within(0.0001));
                Assert.That(xform.Anchored, Is.False);
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.PatternOrigin, Is.EqualTo(axis * (int)Math.Floor(distance + 0.5)));
                foreach (var player in players)
                    Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            }
            var map = entities.GetComponent<TransformComponent>(arena.Owner).MapID;
            server.System<SharedMapSystem>().SetPaused(map, true);
            var frozen = xform.Coordinates;
            system.Update(0);
            Assert.That(xform.Coordinates, Is.EqualTo(frozen));
            server.System<SharedMapSystem>().SetPaused(map, false);
            AdvanceTraversal(server, boss);
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Strike));
            Assert.That((state.Deadline - timing.CurTime).TotalSeconds, Is.EqualTo(3));
            var danger = server.System<BossTelegraphSystem>().SnapshotDanger(boss);
            foreach (var player in players)
                server.System<SharedTransformSystem>().SetCoordinates(player,
                    new EntityCoordinates(arena.Comp.CombatGrid, danger[0].X + 0.5f, danger[0].Y + 0.5f));
            AdvanceTraversal(server, boss);
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Returning));
            foreach (var progress in new[] { 0.2, 0.5, 0.8 })
            {
                state.DashStart = timing.CurTime - TimeSpan.FromSeconds(1.5 * progress);
                state.Deadline = timing.CurTime + TimeSpan.FromSeconds(1.5 * (1 - progress));
                system.Update(0);
                var distance = state.Distance * (1 - progress * progress * (3 - 2 * progress));
                Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Returning));
                Assert.That(xform.Coordinates.X, Is.EqualTo(0.5 + axis.X * distance).Within(0.0001));
                Assert.That(xform.Coordinates.Y, Is.EqualTo(0.5 + axis.Y * distance).Within(0.0001));
                Assert.That(server.System<BossTelegraphSystem>().SnapshotDanger(boss), Is.Empty);
                var returningWarning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(returningWarning.MovementPath, Is.Empty);
                Assert.That(returningWarning.MovementDestination, Is.Null);
            }
            AdvanceTraversal(server, boss);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(xform.Coordinates, Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f)));
            Assert.That(xform.Anchored, Is.True);
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>().Count(), Is.EqualTo(1));
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.EqualTo(1));
        });
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public async Task ConfiguredRainHasShortCastOverlappingThreeSecondAreasRandomSizesAndCompleteDropBudget(int phase)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            server.ResolveDependency<IRobustRandom>().SetSeed(120 + phase);
            var rotation = entities.GetComponent<BossEncounterComponent>(boss);
            var step = new[] { rotation.Tutorial, rotation.Rotation, rotation.Escalation }[phase]
                .Single(step => step.Traversal == BossTraversalKind.CrystalRain);
            var system = server.System<BossTraversalSystem>();
            Assert.That(step.RainCount, Is.EqualTo(new[] { 64, 96, 128 }[phase]));
            Assert.That(system.TryStart(boss, BossTraversalKind.CrystalRain, Hit(1), rainCount: step.RainCount,
                rainWarningTime: step.RainWarningTime, rainSizes: step.RainSizes, rainCastTime: step.RainCastTime,
                rainConcurrent: step.RainConcurrent, rainSpawnInterval: step.RainSpawnInterval,
                rainDoubleChance: step.RainDoubleChance, rainMaxConcurrent: step.RainMaxConcurrent,
                rainMinSpawnInterval: step.RainMinSpawnInterval, rainRampSeconds: step.RainRampSeconds), Is.True);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var bar = entities.GetComponent<BossCastBarComponent>(boss);
            Assert.That((bar.EndTime - bar.StartTime).TotalSeconds, Is.EqualTo(1.5));
            Assert.That(state.RainDrops, Is.Empty);
            AdvanceTraversal(server, boss);
            Assert.That(bar.Casting, Is.False);
            Assert.That(state.RainActiveCap, Is.EqualTo(16));
            Assert.That(state.RainCurrentInterval, Is.EqualTo(0.5));
            var seen = new HashSet<EntityUid>();
            var patterns = new HashSet<BossTelegraphPattern>();
            var peak = 0;
            for (var tick = 0; entities.HasComponent<BossTraversalComponent>(boss) && tick < 1500; tick++)
            {
                peak = Math.Max(peak, state.RainDrops.Count);
                Assert.That(state.RainDrops.Count, Is.LessThanOrEqualTo(state.RainActiveCap));
                Assert.That(state.RainActiveCap, Is.InRange(16, 30));
                Assert.That(state.RainDrops.Select(drop => entities.GetComponent<BossTelegraphComponent>(drop).PatternOrigin).Distinct().Count(), Is.EqualTo(state.RainDrops.Count));
                foreach (var drop in state.RainDrops)
                {
                    var warning = entities.GetComponent<BossTelegraphComponent>(drop);
                    seen.Add(drop);
                    patterns.Add(warning.Pattern);
                    Assert.That((warning.EndTime - warning.StartTime).TotalSeconds, Is.EqualTo(3).Within(0.0001));
                    Assert.That(warning.Danger.Count, Is.EqualTo(warning.Pattern == BossTelegraphPattern.CrystalRainSmall ? 4 : 9));
                    Assert.That(warning.Danger.All(BossTilePatterns.IsPlatform), Is.True);
                }
                foreach (var player in players)
                    server.System<DamageableSystem>().SetAllDamage(player, entities.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
                ElapseRain(server, boss, 0.1);
            }
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(state.Completed, Is.True);
            Assert.That(state.RainResolved, Is.EqualTo(step.RainCount));
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Count(entity =>
                entity.Comp.Boss == boss && entity.Comp.Impact), Is.EqualTo(step.RainCount));
            Assert.That(entities.GetComponent<BossTelegraphSettingsComponent>(boss).CrystalImpactSound, Is.Null);
            Assert.That(entities.AllEntities<AudioComponent>().Any(audio =>
            {
                var fileName = audio.Comp.FileName;
                return fileName.EndsWith("/RevenantBoss/magic_hit.ogg") ||
                    fileName.Contains("/RevenantBoss/crystal_break_");
            }), Is.False,
                "The complete rain must remain silent, including generic magic and crystal break sounds.");
            Assert.That(seen.Count, Is.EqualTo(step.RainCount));
            Assert.That(patterns, Is.EquivalentTo(new[] { BossTelegraphPattern.CrystalRain, BossTelegraphPattern.CrystalRainSmall }));
            Assert.That(peak, Is.GreaterThan(6));
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(entity => entity.Comp.Boss == boss && !entity.Comp.Impact), Is.False);
        });
    }

    [Test]
    public async Task RainOnlyHitsItsDueAreaKeepsOtherWarningsAllowsAutosAndWaitsForFinalDrop()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            server.System<BossFocusMarkerSystem>().SetTank(boss, players[0]);
            var rotation = entities.GetComponent<BossEncounterComponent>(boss);
            rotation.Waiting = true;
            rotation.TankDamage = Hit(18);
            var system = server.System<BossTraversalSystem>();
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(system.TryStart(boss, BossTraversalKind.CrystalRain, Hit(1), rainCount: 8,
                rainDoubleChance: 1, rainSizes: [2, 3]), Is.True);
            rotation.NextTank = now;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            AdvanceTraversal(server, boss);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            ElapseRain(server, boss, 0.5);
            Assert.That(state.RainDrops, Has.Count.EqualTo(4));
            var drop = state.RainDrops[0];
            var warning = entities.GetComponent<BossTelegraphComponent>(drop);
            var unsafeTile = warning.Danger[0];
            var safe = BossTilePatterns.Platform.First(tile => !warning.Danger.Contains(tile));
            for (var i = 0; i < players.Length; i++)
            {
                var tile = i == 0 ? unsafeTile : safe;
                server.System<SharedTransformSystem>().SetCoordinates(players[i],
                    new EntityCoordinates(arena.Comp.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
            }
            system.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            warning.EndTime = now;
            system.Update(0);
            Assert.That(entities.EntityExists(drop), Is.False);
            Assert.That(state.RainDrops, Has.Count.EqualTo(3));
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(1));
            foreach (var player in players.Skip(1))
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            rotation.NextTank = now;
            rotation.NextStep = now;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(1));
            ResolveAutoContact(server, boss);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(19));
            Assert.That(rotation.Waiting, Is.True);
            Assert.That(rotation.Step, Is.Zero);
            while (state.RemainingRain > 0) { state.Deadline = now; system.Update(0); }
            Assert.That(state.RainDrops, Is.Not.Empty);
            Assert.That(rotation.Waiting, Is.True);
            foreach (var player in players)
                server.System<DamageableSystem>().SetAllDamage(player, entities.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
            AdvanceTraversal(server, boss);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(rotation.Waiting, Is.False);
            Assert.That(rotation.Step, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RainPressureRampsFromSixteenToThirtyAndPersistsAcrossCastsWithoutExceedingCap()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var system = server.System<BossTraversalSystem>();
            Assert.That(system.TryStart(boss, BossTraversalKind.CrystalRain, Hit(1), rainCount: 128,
                rainConcurrent: 16, rainMaxConcurrent: 30, rainMinSpawnInterval: 0.15f, rainDoubleChance: 1), Is.True);
            AdvanceTraversal(server, boss);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            state.RainPressureStart = now - TimeSpan.FromSeconds(30);
            system.Update(0);
            Assert.That(state.RainActiveCap, Is.EqualTo(23));
            Assert.That(state.RainCurrentInterval, Is.EqualTo(0.325f).Within(0.0001));
            state.RainPressureStart = now - TimeSpan.FromSeconds(60);
            entities.GetComponent<BossEncounterComponent>(boss).RainPressureStart = state.RainPressureStart;
            for (var i = 0; i < 40; i++) { state.Deadline = now; system.Update(0); }
            Assert.That(state.RainDrops, Has.Count.EqualTo(30));
            Assert.That(state.RainActiveCap, Is.EqualTo(30));
            Assert.That(state.RainCurrentInterval, Is.EqualTo(0.15f).Within(0.0001));
            Assert.That(system.Cancel(boss), Is.True);
            Assert.That(system.TryStart(boss, BossTraversalKind.CrystalRain, Hit(1), rainCount: 128,
                rainConcurrent: 16, rainMaxConcurrent: 30, rainMinSpawnInterval: 0.15f), Is.True);
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<BossTraversalComponent>(boss).RainActiveCap, Is.EqualTo(30));
        });
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public async Task RainWarningsFreezeOnPauseAndClearOnCancelVictoryOrWipe(int ending)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var system = server.System<BossTraversalSystem>();
            Assert.That(system.TryStart(boss, BossTraversalKind.CrystalRain, Hit(1), rainCount: 32, rainDoubleChance: 1), Is.True);
            AdvanceTraversal(server, boss);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var roots = state.RainDrops.ToArray();
            var map = entities.GetComponent<TransformComponent>(arena.Owner).MapID;
            server.System<SharedMapSystem>().SetPaused(map, true);
            foreach (var root in roots) entities.GetComponent<BossTelegraphComponent>(root).EndTime = TimeSpan.Zero;
            system.Update(0);
            Assert.That(state.RainDrops, Is.EqualTo(roots));
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            server.System<SharedMapSystem>().SetPaused(map, false);
            if (ending == 0) system.Cancel(boss);
            else if (ending == 1) server.System<BossSystem>().DamageBoss(boss, 100000);
            else
            {
                foreach (var player in players) server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead);
                system.Update(0);
            }
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            foreach (var root in roots) Assert.That(entities.EntityExists(root), Is.False);
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(entity => entity.Comp.Boss == boss && !entity.Comp.Impact), Is.False);
        });
    }

    private static void ElapseRain(ServerIntegrationInstance server, EntityUid boss, double seconds)
    {
        var entities = server.EntMan;
        var state = entities.GetComponent<BossTraversalComponent>(boss);
        var elapsed = TimeSpan.FromSeconds(seconds);
        state.Deadline -= elapsed;
        state.RainPressureStart -= elapsed;
        if (entities.GetComponent<BossEncounterComponent>(boss).RainPressureStart is { } pressure)
            entities.GetComponent<BossEncounterComponent>(boss).RainPressureStart = pressure - elapsed;
        foreach (var root in state.RainDrops)
        {
            var warning = entities.GetComponent<BossTelegraphComponent>(root);
            warning.StartTime -= elapsed;
            warning.EndTime -= elapsed;
        }
        server.System<BossTraversalSystem>().Update(0);
    }
}
