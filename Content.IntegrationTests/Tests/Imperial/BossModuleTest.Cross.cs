using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossModuleTest
{
    [Test]
    public async Task CrossKeepsSeparateEffectsForItsAvailableTargetedPattern()
    {
        var (server, arena, players) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var catalog = server.System<BossMechanicDefinitionSystem>();
            var spread = catalog.Supports(boss, BossTelegraphPattern.Spread);
            Assert.That(catalog.Supports(boss, BossTelegraphPattern.Cross), Is.True);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss,
                new BossTilePatternAttack { Telegraph = BossTelegraphPattern.Cross, CastTime = 3, Damage = Hit() },
                storedStrike: true, selectedTargets: new[] { players[0] },
                simultaneousAttack: spread ? new BossTilePatternAttack { Telegraph = BossTelegraphPattern.Spread, Damage = Hit() } : null), Is.True);
            var telegraphs = server.System<BossTelegraphSystem>();
            var root = telegraphs.ShowImpact(boss, telegraphs.SnapshotDanger(boss));
            Assert.That(root, Is.Not.Null);
            var effects = server.EntMan.AllEntities<BossTelegraphComponent>()
                .Where(effect => effect.Comp.Boss == boss && effect.Comp.Impact).Select(effect => effect.Comp).ToArray();
            Assert.That(effects, Has.Length.EqualTo(spread ? 2 : 1));
            var cross = effects.Single(effect => effect.Pattern == BossTelegraphPattern.Cross);
            Assert.That(cross.Style, Is.EqualTo(catalog.Style(boss, BossTelegraphPattern.Cross)));
            Assert.That(cross.SpreadCenters, Is.Empty);
            if (spread)
            {
                var targeted = effects.Single(effect => effect.Pattern == BossTelegraphPattern.Spread);
                Assert.That(targeted.Style, Is.EqualTo(catalog.Style(boss, BossTelegraphPattern.Spread)));
                Assert.That(targeted.SpreadCenters, Has.Count.EqualTo(1));
            }
        });
    }
}
