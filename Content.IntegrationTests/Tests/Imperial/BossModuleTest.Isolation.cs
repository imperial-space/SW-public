using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossModuleTest
{
    [Test]
    public async Task ActiveArenasKeepTheirActionsAndHealthSeparate()
    {
        var (server, first, players) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var (second, otherPlayers) = CreateArena(server);
            var entities = server.EntMan;
            var bosses = server.System<BossSystem>();
            var actions = server.System<BossActionSystem>();
            var catalog = server.System<BossMechanicDefinitionSystem>();
            var firstBoss = first.Comp.Boss;
            var secondBoss = second.Comp.Boss;
            var health = entities.GetComponent<BossComponent>(secondBoss).Health;
            bosses.DamageBoss(firstBoss, 10);
            Assert.That(entities.GetComponent<BossComponent>(secondBoss).Health, Is.EqualTo(health));
            Assert.That(server.System<BossFocusMarkerSystem>().SetTank(firstBoss, players[0]), Is.True);
            Assert.That(server.System<BossFocusMarkerSystem>().SetTank(secondBoss, otherPlayers[0]), Is.True);
            var one = entities.GetComponent<BossEncounterComponent>(firstBoss);
            var two = entities.GetComponent<BossEncounterComponent>(secondBoss);
            foreach (var definition in catalog.For(firstBoss).ToArray())
            {
                if (definition.Patterns.Count > 0)
                {
                    var pattern = definition.Patterns[0];
                    var mechanics = server.System<BossMechanicSystem>();
                    Assert.That(mechanics.TryStart(firstBoss, new BossTilePatternAttack { Telegraph = pattern, CastTime = 3, Damage = Hit() }), Is.True);
                    Assert.That(mechanics.TryStart(secondBoss, new BossTilePatternAttack { Telegraph = pattern, CastTime = 3, Damage = Hit() }), Is.True);
                    bosses.CancelBossCast(firstBoss);
                    Assert.That(actions.IsBusy(secondBoss), Is.True, definition.Feature);
                    var warning = server.System<BossTelegraphSystem>().GetTelegraph(secondBoss)!.Value.Comp;
                    Assert.That(warning.Boss, Is.EqualTo(secondBoss));
                    Assert.That(warning.Grid, Is.EqualTo(second.Comp.CombatGrid));
                    bosses.CancelBossCast(secondBoss);
                }
                foreach (var traversal in definition.Traversals)
                {
                    var step = new BossEncounterStep { Traversal = traversal };
                    Assert.That(actions.Start(firstBoss, one, step, Hit()), Is.True);
                    Assert.That(actions.Start(secondBoss, two, step, Hit()), Is.True);
                    bosses.CancelBossCast(firstBoss);
                    Assert.That(actions.IsBusy(secondBoss), Is.True, traversal.ToString());
                    bosses.CancelBossCast(secondBoss);
                }
                if (definition.Attack is { } attack)
                {
                    Assert.That(actions.Start(firstBoss, attack, players), Is.True);
                    Assert.That(actions.Start(secondBoss, attack, otherPlayers), Is.True);
                    bosses.CancelBossCast(firstBoss);
                    Assert.That(actions.IsBusy(secondBoss), Is.True);
                    bosses.CancelBossCast(secondBoss);
                }
            }
            var finalPattern = catalog.For(firstBoss).SelectMany(definition => definition.Patterns)
                .Select(pattern => (BossTelegraphPattern?)pattern).FirstOrDefault();
            if (finalPattern is { } last)
            {
                var mechanics = server.System<BossMechanicSystem>();
                Assert.That(mechanics.TryStart(firstBoss, new BossTilePatternAttack { Telegraph = last, CastTime = 3, Damage = Hit() }), Is.True);
                Assert.That(mechanics.TryStart(secondBoss, new BossTilePatternAttack { Telegraph = last, CastTime = 3, Damage = Hit() }), Is.True);
            }
            server.System<BossEncounterSystem>().End(firstBoss);
            Assert.That(actions.IsBusy(firstBoss), Is.False);
            if (finalPattern != null)
            {
                Assert.That(actions.IsBusy(secondBoss), Is.True);
                bosses.CancelBossCast(secondBoss);
            }
            Assert.That(two.FightRunning, Is.True);
            Assert.That(entities.GetComponent<BossComponent>(secondBoss).Active, Is.True);
        });
    }
}
