using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(2)] [TestCase(3)]
    public async Task RainCrystalUsesApprovedArtHitsOnlyItsFootprintAndFinishesTheLastFade(int size)
    {
        var (server, arena, players, _) = await Setup();
        EntityUid impact = default;
        TimeSpan start = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var telegraphs = server.System<BossTelegraphSystem>();
            var system = server.System<BossTraversalSystem>();
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(system.TryStart(boss, BossTraversalKind.CrystalRain, Hit(1), rainCount: 1,
                rainSizes: [size], rainDoubleChance: 0), Is.True);
            AdvanceTraversal(server, boss);
            var traversal = entities.GetComponent<BossTraversalComponent>(boss);
            var drop = traversal.RainDrops.Single();
            var warning = entities.GetComponent<BossTelegraphComponent>(drop);
            var pattern = size == 2 ? BossTelegraphPattern.CrystalRainSmall : BossTelegraphPattern.CrystalRain;
            Assert.That(warning.Pattern, Is.EqualTo(pattern));
            Assert.That(warning.Danger, Has.Count.EqualTo(size * size));
            var center = BossRainCrystalVisuals.Center(pattern, warning.PatternOrigin);
            Assert.That(center.X, Is.EqualTo(warning.Danger.Average(tile => tile.X + 0.5)));
            Assert.That(center.Y, Is.EqualTo(warning.Danger.Average(tile => tile.Y + 0.5)));
            var style = server.ResolveDependency<IPrototypeManager>().Index(warning.Style);
            Assert.That(telegraphs.HasCrystalAnimation(boss, pattern), Is.True);
            Assert.That(BossRainCrystalVisuals.Texture(style, pattern, false)?.ToString(),
                Does.EndWith(size == 2 ? "rain_small_warning.rsi" : "rain_large_warning.rsi"));
            var danger = warning.Danger.ToArray();
            var origin = warning.PatternOrigin;
            var safe = BossTilePatterns.Platform.First(tile => !warning.Danger.Contains(tile));
            for (var index = 0; index < players.Length; index++)
            {
                var tile = index == 0 ? danger[0] : safe;
                server.System<SharedTransformSystem>().SetCoordinates(players[index],
                    new EntityCoordinates(arena.Comp.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
            }
            system.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            warning.EndTime = server.ResolveDependency<IGameTiming>().CurTime;
            system.Update(0);
            Assert.That(entities.EntityExists(drop), Is.False);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(traversal.Completed, Is.True);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Boss == boss && entry.Comp.Impact);
            impact = effect.Owner;
            start = effect.Comp.StartTime;
            Assert.That(effect.Comp.Pattern, Is.EqualTo(pattern));
            Assert.That(effect.Comp.PatternOrigin, Is.EqualTo(origin));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
            Assert.That(BossRainCrystalVisuals.Texture(style, pattern, true)?.ToString(),
                Does.EndWith(size == 2 ? "rain_small_shatter.rsi" : "rain_large_shatter.rsi"));
            Assert.That((effect.Comp.EndTime - start).TotalSeconds, Is.EqualTo(1.2).Within(0.0001));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(impact).Lifetime, Is.EqualTo(1.2f));
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(entry => entry.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
            for (var index = 0; index < players.Length; index++)
                Assert.That(entities.GetComponent<DamageableComponent>(players[index]).TotalDamage.Float(), Is.EqualTo(index == 0 ? 1 : 0));
            server.System<SharedMapSystem>().SetPaused(entities.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TimedDespawnComponent>(impact).Lifetime, Is.EqualTo(1.2f));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That(server.EntMan.GetComponent<BossTelegraphComponent>(impact).StartTime, Is.GreaterThan(start));
        });
        await server.WaitRunTicks(30);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(impact), Is.True));
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(impact), Is.False));
    }

    [Test]
    public async Task RainCrystalCancellationRemovesBothPendingCrystalsAndExistingShatter()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var system = server.System<BossTraversalSystem>();
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(system.TryStart(boss, BossTraversalKind.CrystalRain, Hit(1), rainCount: 8,
                rainSizes: [2, 3], rainDoubleChance: 1), Is.True);
            AdvanceTraversal(server, boss);
            var state = entities.GetComponent<BossTraversalComponent>(boss);
            var drops = state.RainDrops.ToArray();
            entities.GetComponent<BossTelegraphComponent>(drops[0]).EndTime = server.ResolveDependency<IGameTiming>().CurTime;
            system.Update(0);
            Assert.That(state.RainVisuals, Has.Count.EqualTo(1));
            var shatter = state.RainVisuals.Single();
            Assert.That(entities.EntityExists(shatter), Is.True);
            system.Cancel(boss);
            Assert.That(entities.EntityExists(shatter), Is.False);
            foreach (var drop in drops) Assert.That(entities.EntityExists(drop), Is.False);
        });
    }
}
