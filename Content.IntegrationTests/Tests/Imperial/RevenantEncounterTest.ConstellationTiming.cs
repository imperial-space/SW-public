using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task ConstellationIntroducesSeparateWavesThenKeepsLaterOverlappingWarnings(bool introduction)
    {
        var (server, arena, players, _) = await Setup();
        var boss = arena.Comp.Boss;
        bool[] purpleHits = [];
        bool[] blueHits = [];
        EntityUid[] Flames(bool blue) => server.EntMan.AllEntities<MetaDataComponent>()
            .Where(entity => entity.Comp.EntityPrototype?.ID == (blue ? "RevenantBossBlueFlameEmitter" : "RevenantBossPurpleFlameEmitter"))
            .Select(entity => entity.Owner).ToArray();

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), boss), Is.True);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            var constellations = rotation.Tutorial.Concat(rotation.Rotation).Concat(rotation.Escalation)
                .Where(step => BossTilePatterns.IsConstellation(step.Pattern)).ToArray();
            Assert.That(constellations, Has.Length.EqualTo(3));
            for (var index = 0; index < constellations.Length; index++)
            {
                var step = constellations[index];
                Assert.That(step.Pattern, Is.EqualTo(BossTelegraphPattern.PurpleFirst));
                Assert.That(step.Alternate, Is.Null);
                Assert.That(step.CastTime, Is.EqualTo(4f));
                Assert.That(step.SequentialConstellation, Is.EqualTo(index == 0));
                Assert.That(step.FollowupDelay, Is.EqualTo(index == 0 ? 4f : 2f));
            }
            rotation.Damage["constellation"] = Hit(1);
            rotation.Repeating = !introduction;
            rotation.Step = (introduction ? rotation.Tutorial : rotation.Rotation)
                .FindIndex(step => BossTilePatterns.IsConstellation(step.Pattern));
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            Assert.That((warning.RevealTime - warning.StartTime).TotalSeconds, Is.EqualTo(introduction ? 4 : 2));
            Assert.That((warning.EndTime - warning.RevealTime).TotalSeconds, Is.EqualTo(introduction ? 0 : 2));
            Assert.That(Flames(false), Has.Length.EqualTo(12));
            Assert.That(Flames(true), Is.Empty);
            var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
            Assert.That(bar.Casting && bar.Visible, Is.True);
            Assert.That(bar.CastName, Is.EqualTo(Loc.GetString("Revenant_Boss_Constellation")));
            purpleHits = players.Select(player => server.System<BossTelegraphSystem>().TryGetPlayerTile(boss, player, out var tile)
                && warning.Danger.Contains(tile)).ToArray();
            var blue = BossTilePatterns.Danger(BossTelegraphPattern.PurpleFirst, [], secondPhase: true).ToHashSet();
            blueHits = players.Select(player => server.System<BossTelegraphSystem>().TryGetPlayerTile(boss, player, out var tile)
                && blue.Contains(tile)).ToArray();
        });

        await server.WaitRunTicks(55);
        await server.WaitAssertion(() =>
        {
            Assert.That(Flames(true), Is.Empty);
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(Flames(false), Has.Length.EqualTo(12));
            Assert.That(Flames(true), introduction ? Is.Empty : Has.Length.EqualTo(12));
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.ShowSecondColor, Is.EqualTo(!introduction));
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            Assert.That(Flames(false), Is.Empty);
            Assert.That(Flames(true), Has.Length.EqualTo(12));
            var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
            Assert.That(mechanic.SecondPhase, Is.True);
            Assert.That((mechanic.Deadline - mechanic.Start).TotalSeconds, Is.EqualTo(introduction ? 4 : 2));
            var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
            Assert.That(bar.Casting || bar.Visible, Is.False);
            Assert.That(bar.CastName, Is.Empty);
            for (var i = 0; i < players.Length; i++)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(),
                    Is.EqualTo(purpleHits[i] ? 1 : 0));
        });
        await server.WaitRunTicks(45);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss), Is.True);
            for (var i = 0; i < players.Length; i++)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(),
                    Is.EqualTo(purpleHits[i] ? 1 : 0), "The extra half-second must remain safe from the second hit.");
        });
        await server.WaitRunTicks(introduction ? 75 : 15);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss), Is.False);
            Assert.That(Flames(false).Concat(Flames(true)), Is.Empty);
            Assert.That(server.EntMan.AllEntities<MetaDataComponent>()
                .Any(entity => entity.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False,
                "Constellation retains its existing laser effect.");
            for (var i = 0; i < players.Length; i++)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(),
                    Is.EqualTo((purpleHits[i] ? 1 : 0) + (blueHits[i] ? 1 : 0)));
        });
    }
}
