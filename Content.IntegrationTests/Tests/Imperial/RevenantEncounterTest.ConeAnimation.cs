using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public async Task ApprovedConeStartsAtTheOutsideStrikeAndRetainsItsFadeAfterReturn(int direction)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var traversal = server.System<BossTraversalSystem>();
            var telegraphs = server.System<BossTelegraphSystem>();
            Assert.That(traversal.TryStart(boss, BossTraversalKind.EdgeCone, Hit(1), direction: direction), Is.True);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            var warning = telegraphs.GetTelegraph(boss)!.Value.Comp;
            var danger = warning.Danger.ToArray();
            var origin = BossTraversalSystem.DirectionTile(direction) * 7;
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(e => e.Comp.Impact), Is.False);
            AdvanceTraversal(server, boss);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That(effect.Comp.Pattern, Is.EqualTo(BossTelegraphPattern.CardinalCone));
            Assert.That(effect.Comp.PatternOrigin, Is.EqualTo(origin));
            Assert.That(effect.Comp.Facing, Is.EqualTo((4 - direction) % 4));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
            Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds, Is.EqualTo(2.2).Within(.00001));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime, Is.EqualTo(2.2f).Within(.00001));
            var style = server.ResolveDependency<IPrototypeManager>().Index(effect.Comp.Style);
            Assert.That(BossRiverVisuals.Texture(style, effect.Comp.Pattern, effect.Comp.PatternOrigin,
                effect.Comp.HalfSize, effect.Comp.Facing)?.ToString(),
                Does.EndWith("cone_rivers_" + new[] { "north", "east", "south", "west" }[direction] + ".rsi"));
            var primary = BossTilePatterns.Danger(effect.Comp.Pattern, [], facing: effect.Comp.Facing, origin: origin).ToHashSet();
            var extra = danger.Where(tile => !primary.Contains(tile)).ToArray();
            var hits = entities.AllEntities<MetaDataComponent>().Where(e => e.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect").ToArray();
            Assert.That(hits.Length, Is.EqualTo(extra.Length), "Keep simultaneous flank hits without tile particles over the approved cone.");
            AdvanceTraversal(server, boss);
            Assert.That(telegraphs.GetTelegraph(boss), Is.Null);
            Assert.That(entities.EntityExists(effect.Owner), Is.True, "The return must not cut off the cone's fade.");
        });
    }

    [Test]
    public async Task ApprovedConeHarmlessPreviewUsesTheActualConeWithoutDamage()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss,
                new BossTargetedAttack { Telegraph = BossTelegraphPattern.CardinalCone, CastTime = 3 }, [boss], false), Is.True);
            var telegraphs = server.System<BossTelegraphSystem>();
            telegraphs.SetTraversalFootprint(boss, new Vector2i(0, 7), 0, []);
            telegraphs.ShowCleavePreviewImpact(boss);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds, Is.EqualTo(2.2).Within(.00001));
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.False);
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }
}
