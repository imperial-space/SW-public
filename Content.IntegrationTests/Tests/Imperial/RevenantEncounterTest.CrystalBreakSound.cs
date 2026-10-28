using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(BossTelegraphPattern.CrystalRainSmall)] [TestCase(BossTelegraphPattern.CrystalRain)]
    public async Task FallingCrystalsStaySilentDuringFormationAndImpactWithoutMagicFallback(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var settings = entities.GetComponent<BossTelegraphSettingsComponent>(boss);
            Assert.That(settings.CrystalImpactSound, Is.Null);
            Assert.That(settings.MagicImpactSound, Is.Not.Null);
            var visuals = server.System<BossTelegraphSystem>();
            var audioCount = entities.AllEntities<AudioComponent>().Count();
            var warning = visuals.CreateIndependentWarning(boss, pattern, Vector2i.Zero,
                server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(3));
            Assert.That(entities.AllEntities<AudioComponent>().Count(), Is.EqualTo(audioCount),
                "Falling crystal formation must stay silent.");
            entities.GetComponent<BossTelegraphComponent>(warning).EndTime = server.ResolveDependency<IGameTiming>().CurTime;
            visuals.ShowImpact(boss, entities.GetComponent<BossTelegraphComponent>(warning).Danger, warningEntity: warning);
            Assert.That(entities.AllEntities<AudioComponent>().Count(), Is.EqualTo(audioCount),
                "Falling crystal impacts must stay silent even when generic magic audio is configured.");
        });
    }
}
