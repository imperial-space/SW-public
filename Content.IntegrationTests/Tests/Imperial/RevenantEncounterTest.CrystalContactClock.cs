using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(0)] [TestCase(.15)] [TestCase(.6)] [TestCase(1.4)]
    public async Task LateCrystalImpactKeepsTheWarningContactClockWithoutReplayingTheLanding(double late)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            var visuals = server.System<BossTelegraphSystem>();
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            var deadline = now - TimeSpan.FromSeconds(late);
            var warning = visuals.CreateIndependentWarning(boss, BossTelegraphPattern.CrystalRain, Vector2i.Zero, deadline);
            entities.GetComponent<BossTelegraphComponent>(warning).StartTime = deadline - TimeSpan.FromSeconds(3);
            var root = visuals.ShowImpact(boss, entities.GetComponent<BossTelegraphComponent>(warning).Danger,
                warningEntity: warning, playSound: false)!.Value;
            var impact = entities.GetComponent<BossTelegraphComponent>(root);
            Assert.That(impact.StartTime, Is.EqualTo(deadline));
            Assert.That((impact.EndTime - deadline).TotalSeconds, Is.EqualTo(1.2).Within(.00001));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(root).Lifetime,
                Is.EqualTo(Math.Max(0, 1.2 - late)).Within(.00001));
            var pending = BossRainCrystalVisuals.Playback(3 + late, 3, false);
            Assert.That(BossRainCrystalVisuals.FrameIndex((now - impact.StartTime).TotalSeconds, 1.2, true),
                Is.EqualTo(BossRainCrystalVisuals.FrameIndex(pending.Elapsed, 3, pending.Impact)));
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }
}
