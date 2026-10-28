using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(BossTelegraphPattern.BlueFirst)] [TestCase(BossTelegraphPattern.PurpleFirst)]
    public async Task ConstellationNativeBeamsStartAtEachHitKeepThePhaseAndFade(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var mechanics = server.System<BossMechanicSystem>();
            var visuals = server.System<BossTelegraphSystem>();
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = pattern, CastTime = 4, FollowupDelay = 2, Damage = Hit(1) }), Is.True);
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(e => e.Comp.Impact), Is.False,
                "No beam fires while the flames charge.");
            for (var phase = 0; phase < 2; phase++)
            {
                var warning = visuals.GetTelegraph(boss)!.Value.Comp;
                var danger = warning.Danger.ToArray();
                var before = players.Select(player => entities.GetComponent<DamageableComponent>(player).TotalDamage.Float()).ToArray();
                var hits = players.Select(player => visuals.TryGetPlayerTile(boss, player, out var tile) && danger.Contains(tile)).ToArray();
                entities.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                mechanics.Update(0);
                var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact && e.Comp.SecondPhase == (phase == 1));
                Assert.That(effect.Comp.Pattern, Is.EqualTo(pattern));
                Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
                Assert.That(BossConstellationVisuals.State(effect.Comp.Pattern, effect.Comp.SecondPhase),
                    Is.EqualTo((pattern == BossTelegraphPattern.BlueFirst) != (phase == 1) ? "blue" : "purple"));
                Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds, Is.EqualTo(1.74).Within(.00001));
                Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime, Is.EqualTo(1.74f));
                for (var i = 0; i < players.Length; i++)
                    Assert.That(entities.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(), Is.EqualTo(before[i] + (hits[i] ? 1 : 0)));
                if (phase == 0)
                    Assert.That((entities.GetComponent<BossMechanicComponent>(boss).Deadline - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds,
                        Is.EqualTo(2), "The fire fade does not alter the movement window.");
            }
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(e => e.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
        });
    }

    [TestCase(BossTelegraphPattern.BlueFirst)] [TestCase(BossTelegraphPattern.PurpleFirst)]
    public async Task ConstellationNativeBeamPreviewFiresBothPhasesSilentlyAndWithoutDamage(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        var boss = arena.Comp.Boss;
        var rate = server.ResolveDependency<IGameTiming>().TickRate;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss,
                new BossTargetedAttack { Telegraph = pattern, CastTime = 6 }, [boss], false), Is.True);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            warning.SwitchTime = now + TimeSpan.FromSeconds(.1);
            warning.EndTime = now + TimeSpan.FromSeconds(.7);
            server.EntMan.GetComponent<BossCastBarComponent>(boss).EndTime = warning.EndTime;
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>().Any(e => e.Comp.Impact), Is.False);
        });
        await server.WaitRunTicks((int)Math.Ceiling(rate * .3));
        await server.WaitAssertion(() =>
        {
            var effect = server.EntMan.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That(effect.Comp.SecondPhase, Is.False);
            Assert.That(effect.Comp.Pattern, Is.EqualTo(pattern));
        });
        await server.WaitRunTicks((int)Math.Ceiling(rate * .6));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>().Count(e => e.Comp.Impact), Is.EqualTo(2));
            Assert.That(server.EntMan.AllEntities<AudioComponent>().Any(e =>
            {
                var file = e.Comp.FileName;
                return file.Contains("/RevenantBoss/");
            }), Is.False);
            Assert.That(server.EntMan.GetComponent<BossComponent>(boss).Active, Is.False);
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }

    [Test]
    public async Task ConstellationNativeBeamRetainsItsFadeAcrossPauseAndExpires()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid effect = default;
        TimeSpan originalStart = default;
        var rate = server.ResolveDependency<IGameTiming>().TickRate;
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss,
                new BossTilePatternAttack { Telegraph = BossTelegraphPattern.PurpleFirst, CastTime = 5, FollowupDelay = 2, Damage = Hit(1) }), Is.True);
            var visuals = server.System<BossTelegraphSystem>();
            effect = visuals.ShowImpact(boss, visuals.SnapshotDanger(boss), playSound: false)!.Value;
            originalStart = server.EntMan.GetComponent<BossTelegraphComponent>(effect).StartTime;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(rate * 2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime, Is.EqualTo(1.74f));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That(server.EntMan.GetComponent<BossTelegraphComponent>(effect).StartTime, Is.GreaterThan(originalStart));
        });
        await server.WaitRunTicks((int)Math.Floor(rate * .8));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(effect), Is.True);
            Assert.That(server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime, Is.GreaterThan(.25f));
        });
        await server.WaitRunTicks((int)Math.Ceiling(rate * 1.0));
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(effect), Is.False));
    }
}
