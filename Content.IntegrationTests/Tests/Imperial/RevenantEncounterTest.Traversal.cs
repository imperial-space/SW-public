using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.ContentPack;
using System.Text.Json;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task ConfiguredCastAnimationsResolveTexturePathsAndBothConfiguredAnimatedStates()
    {
        var (server, arena, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var presentation = server.EntMan.GetComponent<BossPresentationComponent>(arena.Comp.Boss);
            var resources = server.ResolveDependency<IResourceManager>();
            var paths = new[] { "/Textures/Imperial/Medieval/SpellCastEffects/darkness/tentacles.rsi",
                "/Textures/Imperial/Medieval/SpellCastEffects/MultiCurrency/wave.rsi" };
            for (var i = 0; i < 2; i++)
            {
                var effect = presentation.GetCastEffect(i == 1)!;
                Assert.That(effect.RsiPath.ToString(), Is.EqualTo(paths[i]));
                using var metadata = JsonDocument.Parse(resources.ContentFileReadAllText(effect.RsiPath / "meta.json"));
                var state = metadata.RootElement.GetProperty("states").EnumerateArray()
                    .Single(state => state.GetProperty("name").GetString() == effect.RsiState);
                Assert.That(state.GetProperty("delays")[0].GetArrayLength(), Is.GreaterThan(1));
                Assert.That(resources.ContentFileExists(effect.RsiPath / (effect.RsiState + ".png")), Is.True);
            }
            Assert.That(presentation.CastEffectScale, Is.EqualTo(3));
        });
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public async Task CrossPreviewsThenAcceleratesFourTilesWaitsHalfSecondBeforeHittingAndReturnsWithoutArrows(int direction)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var system = server.System<BossTraversalSystem>();
            Assert.That(system.TryStart(boss, BossTraversalKind.StepCross, Hit(1), direction: direction), Is.True);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var timing = server.ResolveDependency<IGameTiming>();
            var xform = entities.GetComponent<TransformComponent>(boss);
            var axis = BossTraversalSystem.DirectionTile(direction);
            var telegraphs = server.System<BossTelegraphSystem>();
            var warning = telegraphs.GetTelegraph(boss)!.Value.Comp;
            Assert.That(state.Distance, Is.EqualTo(4));
            Assert.That((state.Deadline - timing.CurTime).TotalSeconds, Is.EqualTo(3));
            Assert.That(warning.Pattern, Is.EqualTo(BossTelegraphPattern.Cross));
            Assert.That(warning.MovementDestination, Is.EqualTo(axis * 4));
            Assert.That(warning.MovementPath, Is.EqualTo(Enumerable.Range(0, 5).Select(i => axis * i)));
            state.Deadline = timing.CurTime + TimeSpan.FromSeconds(0.1);
            system.Update(0);
            Assert.That(xform.Coordinates, Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f)));
            AdvanceTraversal(server, boss);
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Dashing));
            Assert.That((state.Deadline - state.DashStart).TotalSeconds, Is.EqualTo(0.6).Within(0.0001));
            Assert.That((entities.GetComponent<BossCastBarComponent>(boss).EndTime - state.Deadline).TotalSeconds,
                Is.EqualTo(0.5).Within(0.0001));
            foreach (var progress in new[] { 0.1, 0.25, 0.5, 0.75, 0.9, 0.99 })
            {
                state.DashStart = timing.CurTime - TimeSpan.FromSeconds(0.6 * progress);
                state.Deadline = timing.CurTime + TimeSpan.FromSeconds(0.6 * (1 - progress));
                system.Update(0);
                var distance = progress * progress * (3 - 2 * progress) * 4;
                Assert.That(xform.Coordinates.X, Is.EqualTo(0.5 + axis.X * distance).Within(0.0001));
                Assert.That(xform.Coordinates.Y, Is.EqualTo(0.5 + axis.Y * distance).Within(0.0001));
                Assert.That(xform.Anchored, Is.False);
                var tile = (int)Math.Floor(distance + 0.5);
                Assert.That(warning.PatternOrigin, Is.EqualTo(axis * tile));
                Assert.That(warning.Danger, Is.EquivalentTo(BossTilePatterns.Danger(BossTelegraphPattern.Cross, [], origin: axis * tile)));
                foreach (var player in players)
                    Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            }
            var tangent = BossTraversalSystem.DirectionTile((direction + 1) % 4);
            Assert.That(warning.Danger, Does.Contain(axis * 5 + tangent * 5));
            Assert.That(warning.Danger, Does.Contain(axis * 5 - tangent * 5));
            foreach (var player in players)
                server.System<SharedTransformSystem>().SetCoordinates(player,
                    new EntityCoordinates(arena.Comp.CombatGrid, axis.X * 5 + tangent.X * 5 + 0.5f, axis.Y * 5 + tangent.Y * 5 + 0.5f));
            AdvanceTraversal(server, boss);
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Strike));
            Assert.That((state.Deadline - timing.CurTime).TotalSeconds, Is.EqualTo(0.5));
            Assert.That(warning.EndTime, Is.EqualTo(state.Deadline));
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).EndTime, Is.EqualTo(state.Deadline));
            Assert.That(warning.PatternOrigin, Is.EqualTo(axis * 4));
            var landingPosition = xform.Coordinates;
            state.Deadline = timing.CurTime + TimeSpan.FromSeconds(0.25);
            system.Update(0);
            Assert.That(xform.Coordinates, Is.EqualTo(landingPosition));
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Strike));
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            AdvanceTraversal(server, boss);
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Returning));
            Assert.That(xform.Coordinates, Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, axis.X * 4 + 0.5f, axis.Y * 4 + 0.5f)));
            Assert.That(telegraphs.SnapshotDanger(boss), Is.Empty);
            Assert.That(telegraphs.GetTelegraph(boss)!.Value.Comp.MovementDestination, Is.Null);
            Assert.That(telegraphs.GetTelegraph(boss)!.Value.Comp.MovementPath, Is.Empty);
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.EqualTo(1));
            state.DashStart = timing.CurTime - TimeSpan.FromSeconds(1);
            state.Deadline = timing.CurTime + TimeSpan.FromSeconds(1);
            system.Update(0);
            Assert.That(xform.Coordinates.X, Is.EqualTo(0.5 + axis.X * 2).Within(0.0001));
            Assert.That(xform.Coordinates.Y, Is.EqualTo(0.5 + axis.Y * 2).Within(0.0001));
            Assert.That(telegraphs.GetTelegraph(boss)!.Value.Comp.MovementDestination, Is.Null);
            Assert.That(telegraphs.GetTelegraph(boss)!.Value.Comp.MovementPath, Is.Empty);
            AdvanceTraversal(server, boss);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(xform.Coordinates, Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f)));
            Assert.That(xform.Anchored, Is.True);
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>().Count(), Is.EqualTo(1));
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.EqualTo(1));
        });
    }

    [TestCase(false, false)] [TestCase(true, false)]
    [TestCase(false, true)] [TestCase(true, true)]
    public async Task InterruptedCrossDashOrLandingDelayRestoresCenterWithoutDamageOrBlockerAfterVictory(bool victory, bool landed)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var system = server.System<BossTraversalSystem>();
            Assert.That(system.TryStart(boss, BossTraversalKind.StepCross, Hit(1), direction: 0), Is.True);
            AdvanceTraversal(server, boss);
            var timing = server.ResolveDependency<IGameTiming>();
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            state.DashStart = timing.CurTime - TimeSpan.FromSeconds(0.3);
            state.Deadline = timing.CurTime + TimeSpan.FromSeconds(0.3);
            system.Update(0);
            if (landed)
            {
                AdvanceTraversal(server, boss);
                Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Strike));
            }
            var map = entities.GetComponent<TransformComponent>(arena.Owner).MapID;
            server.System<SharedMapSystem>().SetPaused(map, true);
            var position = entities.GetComponent<TransformComponent>(boss).Coordinates;
            state.Deadline = timing.CurTime;
            system.Update(0);
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates, Is.EqualTo(position));
            server.System<SharedMapSystem>().SetPaused(map, false);
            if (victory) server.System<BossSystem>().DamageBoss(boss, 100000);
            else server.System<BossSystem>().CancelBossCast(boss);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates, Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f)));
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>().Any(), Is.EqualTo(!victory));
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }

    private static void AdvanceTraversal(ServerIntegrationInstance server, EntityUid boss)
    {
        var state = server.EntMan.GetComponent<BossTraversalComponent>(boss);
        var now = server.ResolveDependency<IGameTiming>().CurTime;
        state.Deadline = now;
        foreach (var drop in state.RainDrops)
            server.EntMan.GetComponent<BossTelegraphComponent>(drop).EndTime = now;
        server.System<BossTraversalSystem>().Update(0);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public async Task MovingCleaveAvoidsThirdRepeatAndRecordsOnlyItsResolvedStrike(int direction)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var sequences = server.System<BossSequenceSystem>();
            var blocked = BossTraversalSystem.Cleave((direction + 2) % 4);
            sequences.RecordStrike(boss, blocked);
            sequences.RecordStrike(boss, blocked);
            var system = server.System<BossTraversalSystem>();
            Assert.That(system.TryStart(boss, BossTraversalKind.StepCleave, Hit(1), direction: direction), Is.False);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(system.TryStart(boss, BossTraversalKind.StepCleave, Hit(1), distance: 2), Is.True);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var selected = state.Pattern;
            Assert.That(selected, Is.Not.EqualTo(blocked));
            Assert.That(entities.GetComponent<BossSequenceHistoryComponent>(boss).LastStrikes, Is.EqualTo(new[] { blocked, blocked }));
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<BossSequenceHistoryComponent>(boss).LastStrikes, Is.EqualTo(new[] { blocked, selected }));
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<BossSequenceHistoryComponent>(boss).LastStrikes, Is.EqualTo(new[] { blocked, selected }));
            Assert.That(sequences.CanFollowHistory(boss, [selected, selected]), Is.False);
        });
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public async Task EdgeFlankAvoidsThirdRepeatUsesExpandedNameAndRecordsItsStrike(int direction)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var sequences = server.System<BossSequenceSystem>();
            var blocked = BossTraversalSystem.Cleave((direction + 1) % 4);
            var selected = BossTraversalSystem.Cleave((direction + 3) % 4);
            sequences.RecordStrike(boss, blocked);
            sequences.RecordStrike(boss, blocked);
            Assert.That(server.System<BossTraversalSystem>().TryStart(boss, BossTraversalKind.EdgeCone, Hit(1), flank: true, direction: direction), Is.True);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<BossTraversalComponent>(boss).FlankPattern, Is.EqualTo(selected));
            Assert.That(entities.GetComponent<BossSequenceHistoryComponent>(boss).LastStrikes, Is.EqualTo(new[] { blocked, blocked }));
            var bar = entities.GetComponent<BossCastBarComponent>(boss);
            Assert.That(bar.CastNameKey, Does.StartWith("Revenant_Boss_Edge_Slash_"));
            Assert.That(bar.GetLocalizedCastName(), Does.StartWith("Judicium Ultra Limen"));
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<BossSequenceHistoryComponent>(boss).LastStrikes, Is.EqualTo(new[] { blocked, selected }));
        });
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public async Task EdgeBossRemainsReachableByPreciseAndWideMeleeFromLastPlatformTile(int direction)
    {
        var (server, arena, players, _) = await Setup();
        var boss = arena.Comp.Boss;
        await server.WaitAssertion(() =>
        {
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossTraversalSystem>().TryStart(boss, BossTraversalKind.EdgeCone, Hit(1), direction: direction), Is.True);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            var tile = BossTraversalSystem.DirectionTile(direction) * 5;
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
            server.System<SharedCombatModeSystem>().SetInCombatMode(players[0], true);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var health = entities.GetComponent<BossComponent>(boss);
            var weapon = entities.GetComponent<MeleeWeaponComponent>(players[0]);
            var before = health.Health;
            Assert.That(server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(players[0], players[0], weapon, boss), Is.True);
            Assert.That(health.Health, Is.LessThan(before));
            before = health.Health;
            weapon.NextAttack = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(server.System<SharedMeleeWeaponSystem>().AttemptHeavyAttack(players[0], players[0], weapon,
                entities.GetComponent<TransformComponent>(boss).Coordinates), Is.True);
            Assert.That(health.Health, Is.LessThan(before));
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task TraversalInterruptionClearsWarningsAndDoesNotRestoreCenterAfterVictory(bool victory)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossTraversalSystem>().TryStart(boss, BossTraversalKind.EdgeCone, Hit(1), direction: 0), Is.True);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            if (victory) server.System<BossSystem>().DamageBoss(boss, 100000);
            else server.System<BossSystem>().CancelBossCast(boss);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>().Any(), Is.EqualTo(!victory));
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
        });
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public async Task EdgeConeAppearsOutsideAndOnlySlashesAnOrthogonalSafeSide(int direction)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var system = server.System<BossTraversalSystem>();
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            Assert.That(system.TryStart(boss, BossTraversalKind.EdgeCone, encounter.Damage["cone"],
                flank: true, direction: direction, flankDamage: encounter.Damage["cleave"]), Is.True);
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>(), Is.Empty);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss,
                new BossTilePatternAttack { Telegraph = BossTelegraphPattern.Cross, Damage = Hit(1) }), Is.False);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var outside = BossTraversalSystem.DirectionTile(direction) * 7;
            Assert.That(state.Origin, Is.EqualTo(outside));
            Assert.That(BossTilePatterns.IsPlatform(state.Origin), Is.False);
            var transformSystem = server.System<SharedTransformSystem>();
            var position = transformSystem.ToCoordinates(arena.Comp.CombatGrid, transformSystem.GetMapCoordinates(boss));
            Assert.That(position.EntityId, Is.EqualTo(arena.Comp.CombatGrid));
            Assert.That(position.X, Is.EqualTo(outside.X + 0.5f));
            Assert.That(position.Y, Is.EqualTo(outside.Y + 0.5f));
            var telegraphs = server.System<BossTelegraphSystem>();
            var cone = BossTilePatterns.Danger(BossTelegraphPattern.CardinalCone, [],
                facing: (4 - direction) % 4, origin: outside);
            var tangent = BossTraversalSystem.DirectionTile((direction + 1) % 4);
            var pocket1 = BossTraversalSystem.DirectionTile(direction) * 4 + tangent * 4;
            var pocket2 = BossTraversalSystem.DirectionTile(direction) * 4 - tangent * 4;
            Assert.That(cone, Does.Not.Contain(pocket1));
            Assert.That(cone, Does.Not.Contain(pocket2));
            Assert.That(cone, Does.Contain(Vector2i.Zero));
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Strike));
            Assert.That(state.FlankPattern, direction % 2 == 0
                ? Is.AnyOf(BossTelegraphPattern.Dextra, BossTelegraphPattern.Sinistra)
                : Is.AnyOf(BossTelegraphPattern.Frons, BossTelegraphPattern.Tergum));
            var slash = telegraphs.SnapshotDanger(boss);
            Assert.That(slash.Contains(pocket1) ^ slash.Contains(pocket2), Is.True);
            Assert.That((state.Deadline - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds, Is.EqualTo(3));
            var safe = slash.Contains(pocket1) ? pocket2 : pocket1;
            var unsafeTile = slash.Contains(pocket1) ? pocket1 : pocket2;
            var flank = BossTilePatterns.Danger(state.FlankPattern!.Value, [], origin: outside);
            Assert.That(slash, Is.EquivalentTo(cone.Union(flank)));
            var overlap = cone.First(flank.Contains);
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, unsafeTile.X + 0.5f, unsafeTile.Y + 0.5f));
            server.System<SharedTransformSystem>().SetCoordinates(players[1], new EntityCoordinates(arena.Comp.CombatGrid, safe.X + 0.5f, safe.Y + 0.5f));
            server.System<SharedTransformSystem>().SetCoordinates(players[2], new EntityCoordinates(arena.Comp.CombatGrid, overlap.X + 0.5f, overlap.Y + 0.5f));
            foreach (var player in players) server.System<DamageableSystem>().SetAllDamage(player, entities.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
            system.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Slash"].Float(), Is.EqualTo(60));
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Heat"].Float(), Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).Damage.DamageDict["Slash"].Float(), Is.EqualTo(60));
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).Damage.DamageDict["Heat"].Float(), Is.EqualTo(60));
            Assert.That(state.Phase, Is.EqualTo(BossTraversalPhase.Returning));
            AdvanceTraversal(server, boss);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(telegraphs.GetTelegraph(boss), Is.Null);
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>().Count(), Is.EqualTo(1));
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates, Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f)));
        });
    }

    [Test]
    public async Task ConeAndSpreadResolveTogetherAndCancellationRestoresCenter()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var system = server.System<BossTraversalSystem>();
            Assert.That(system.TryStart(boss, BossTraversalKind.EdgeCone, Hit(1), spread: true, spreadDamage: Hit(2), direction: 0), Is.True);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var marked = state.Targets.Single();
            var victim = players.First(player => player != marked);
            var transform = server.System<SharedTransformSystem>();
            transform.SetCoordinates(marked, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            transform.SetCoordinates(victim, new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            Assert.That(entities.AllEntities<BossActorMarkerComponent>().Count(mark => mark.Comp.Kind == BossActorMarkerKind.Spread), Is.EqualTo(1));
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<DamageableComponent>(victim).TotalDamage.Float(), Is.Zero);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            Assert.That(entities.GetComponent<DamageableComponent>(victim).TotalDamage.Float(), Is.EqualTo(3));
            Assert.That(entities.AllEntities<BossActorMarkerComponent>().Any(mark => mark.Comp.Kind == BossActorMarkerKind.Spread), Is.False);
            AdvanceTraversal(server, boss);
            Assert.That(system.TryStart(boss, BossTraversalKind.EdgeCone, Hit(1), direction: 3), Is.True);
            AdvanceTraversal(server, boss);
            Assert.That(server.System<BossSystem>().CancelBossCast(boss), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates, Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f)));
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>().Count(), Is.EqualTo(1));
        });
    }

}
