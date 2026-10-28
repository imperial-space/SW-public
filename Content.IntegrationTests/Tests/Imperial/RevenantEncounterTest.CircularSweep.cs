using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Components;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(BossTelegraphPattern.Rejection)] [TestCase(BossTelegraphPattern.Invitation)]
    public async Task CircularSweepUsesTheCorrectArtworkForBothConfirmedImpactsWithoutChangingDamage(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            var timing = server.ResolveDependency<IGameTiming>();
            var telegraphs = server.System<BossTelegraphSystem>();
            var mechanics = server.System<BossMechanicSystem>();
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            for (var index = 0; index < players.Length; index++)
                server.System<SharedTransformSystem>().SetCoordinates(players[index],
                    index % 2 == 0 ? new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 2.5f) :
                        new EntityCoordinates(arena.Comp.CombatGrid, 5.5f, 5.5f));
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = pattern, CastTime = 4, FollowupDelay = 2, Damage = Hit(1) }), Is.True);
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(entry => entry.Comp.Impact), Is.False);
            var mechanic = entities.GetComponent<BossMechanicComponent>(boss);
            mechanic.Deadline = timing.CurTime;
            mechanics.Update(0);
            var first = entities.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact);
            Assert.That(first.Comp.SecondPhase, Is.False);
            Assert.That((mechanic.Deadline - timing.CurTime).TotalSeconds, Is.EqualTo(2));
            var firstClose = pattern == BossTelegraphPattern.Rejection;
            for (var index = 0; index < players.Length; index++)
                Assert.That(entities.GetComponent<DamageableComponent>(players[index]).TotalDamage.Float(),
                    Is.EqualTo((index % 2 == 0) == firstClose ? 1 : 0));
            Assert.That(telegraphs.GetTelegraph(boss)!.Value.Comp.SecondPhase, Is.True);
            AssertImpact(first, false);
            mechanic.Deadline = timing.CurTime;
            mechanics.Update(0);
            var second = entities.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact && entry.Comp.SecondPhase);
            AssertImpact(second, true);
            Assert.That(first.Comp.SecondPhase, Is.False, "The first image keeps its frozen phase when the warning switches.");
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.EqualTo(1));
            mechanics.Update(0);
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Count(entry => entry.Comp.Impact), Is.EqualTo(2));
            Assert.That(entities.HasComponent<BossMechanicComponent>(boss), Is.False);

            void AssertImpact(Entity<BossTelegraphComponent> effect, bool secondPhase)
            {
                var style = server.ResolveDependency<IPrototypeManager>().Index(effect.Comp.Style);
                var close = firstClose != secondPhase;
                Assert.That(BossCircularSweepVisuals.Texture(style, pattern, secondPhase)?.ToString(),
                    Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/" + (close ? "close_sweep.png" : "donut_sweep.png")));
                Assert.That(effect.Comp.Danger, Is.EquivalentTo(BossTilePatterns.Danger(pattern, [], secondPhase: secondPhase)));
                Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds, Is.EqualTo(0.7).Within(0.00001));
                Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime, Is.EqualTo(0.7).Within(0.00001));
                Assert.That(entities.AllEntities<MetaDataComponent>().Any(entry =>
                    entry.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
            }
        });
    }

    [TestCase(BossTelegraphPattern.Rejection)] [TestCase(BossTelegraphPattern.Invitation)]
    public async Task CircularSweepHarmlessPreviewShowsOnlyAnImpactAndDealsNoDamage(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossTelegraphSystem>().Prepare(boss,
                new BossTargetedAttack { Telegraph = pattern, CastTime = 3 }, [boss], false);
            server.System<BossTelegraphSystem>().ShowCleavePreviewImpact(boss);
            var effect = server.EntMan.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact);
            Assert.That(effect.Comp.EndTime, Is.GreaterThan(effect.Comp.StartTime));
            Assert.That(server.EntMan.GetComponent<BossComponent>(boss).Active, Is.False);
            Assert.That(server.EntMan.AllEntities<AudioComponent>().Any(entry =>
            {
                var file = entry.Comp.FileName;
                return file.EndsWith("/RevenantBoss/slash.ogg");
            }), Is.False);
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }

    [Test]
    public async Task CircularSweepPausesWithTheArenaAndExpiresAfterUnpause()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid effect = default;
        TimeSpan start = default;
        float lifetime = 0;
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Rejection, CastTime = 4, Damage = Hit(1) });
            server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            effect = server.EntMan.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact).Owner;
            start = server.EntMan.GetComponent<BossTelegraphComponent>(effect).StartTime;
            lifetime = server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime, Is.EqualTo(lifetime));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That(server.EntMan.GetComponent<BossTelegraphComponent>(effect).StartTime, Is.GreaterThan(start));
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(effect), Is.False));
    }
}
