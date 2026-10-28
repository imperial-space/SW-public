using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task ConstellationOverlapDealsFortyHeatPerWave()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            encounter.Tutorial = [new() { Pattern = BossTelegraphPattern.PurpleFirst, CastTime = 4,
                FollowupDelay = 4, SequentialConstellation = true, Damage = "constellation" }];
            var first = BossTilePatterns.Danger(BossTelegraphPattern.PurpleFirst, []);
            var second = BossTilePatterns.Danger(BossTelegraphPattern.PurpleFirst, [], secondPhase: true);
            var overlap = first.Intersect(second).First();
            server.System<SharedTransformSystem>().SetCoordinates(players[0],
                new EntityCoordinates(arena.Comp.CombatGrid, overlap.X + .5f, overlap.Y + .5f));
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.NextStep = now;
            server.System<BossEncounterSystem>().Update(0);
            var mechanics = server.System<BossMechanicSystem>();
            var mechanic = entities.GetComponent<BossMechanicComponent>(boss);
            mechanic.Deadline = now;
            mechanics.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Heat"].Float(), Is.EqualTo(40));
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Cold"].Float(), Is.Zero);
            mechanic.Deadline = now;
            mechanics.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(80));
            Assert.That(entities.HasComponent<BossMechanicComponent>(boss), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CommunionFogUsesColdDamageForDirectAndAlternatingCasts(bool alternating)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            entities.EnsureComponent<BossFocusMarkerComponent>(boss).Tank = players[0];
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            encounter.Repeating = alternating;
            encounter.Lap = alternating ? 1 : 0;
            encounter.Tutorial = [new() { Pattern = BossTelegraphPattern.Stack, CastTime = 4, Damage = "sentence" }];
            encounter.Rotation = [new() { Pattern = BossTelegraphPattern.Spread, Alternate = BossTelegraphPattern.Stack,
                CastTime = 4, Damage = "sentence" }];
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.NextStep = now;
            server.System<BossEncounterSystem>().Update(0);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            Assert.That(warning.Pattern, Is.EqualTo(BossTelegraphPattern.Stack));
            var unsafeTile = warning.Danger.First();
            server.System<SharedTransformSystem>().SetCoordinates(players[0],
                new EntityCoordinates(arena.Comp.CombatGrid, unsafeTile.X + .5f, unsafeTile.Y + .5f));
            entities.GetComponent<BossMechanicComponent>(boss).Deadline = now;
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Cold"].Float(), Is.EqualTo(100));
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Slash"].Float(), Is.Zero);
        });
    }
}
