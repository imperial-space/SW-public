using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(BossTelegraphPattern.Cross)] [TestCase(BossTelegraphPattern.Divide)]
    public async Task RiverAnimationStartsAtConfirmedHitWithoutChangingDangerOrDamage(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var mechanics = server.System<BossMechanicSystem>();
            var visuals = server.System<BossTelegraphSystem>();
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack { Telegraph = pattern, Damage = Hit(1), CastTime = 3 }), Is.True);
            var warning = visuals.GetTelegraph(boss)!.Value.Comp;
            var danger = warning.Danger.ToArray();
            var safe = warning.Safe.First();
            for (var i = 0; i < players.Length; i++)
            {
                var tile = i == 0 ? danger.First() : safe;
                server.System<SharedTransformSystem>().SetCoordinates(players[i],
                    new EntityCoordinates(arena.Comp.CombatGrid, tile.X + .5f, tile.Y + .5f));
            }
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(e => e.Comp.Impact), Is.False);
            mechanics.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            entities.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That(effect.Comp.Pattern, Is.EqualTo(pattern));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
            Assert.That(effect.Comp.PatternOrigin, Is.EqualTo(Vector2i.Zero));
            Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds, Is.EqualTo(2.2).Within(.00001));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime, Is.EqualTo(2.2f));
            var style = server.ResolveDependency<IPrototypeManager>().Index(effect.Comp.Style);
            Assert.That(BossRiverVisuals.Texture(style, pattern, effect.Comp.PatternOrigin, effect.Comp.HalfSize)?.ToString(),
                Does.EndWith(pattern == BossTelegraphPattern.Cross ? "cross_rivers.rsi" : "divide_rivers.rsi"));
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(e => e.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(1));
            foreach (var player in players.Skip(1))
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            mechanics.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(1));
        });
    }

    [TestCase(BossTelegraphPattern.Cross)] [TestCase(BossTelegraphPattern.Divide)]
    public async Task RiverAnimationHarmlessPreviewRemainsSilentAndDealsNoDamage(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss,
                new BossTargetedAttack { Telegraph = pattern, CastTime = 3 }, [boss], false), Is.True);
            entities.GetComponent<BossCastBarComponent>(boss).EndTime = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossSystem>().Update(0);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That(effect.Comp.Pattern, Is.EqualTo(pattern));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime, Is.EqualTo(2.2f));
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.False);
            Assert.That(entities.AllEntities<AudioComponent>().Any(e =>
            {
                var file = e.Comp.FileName;
                return file.Contains("/RevenantBoss/");
            }), Is.False);
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }

    [TestCase(0, "north")] [TestCase(1, "east")] [TestCase(2, "south")] [TestCase(3, "west")]
    public async Task RiverAnimationMovingCrossKeepsItsFrozenLandingOriginAndActualRim(int direction, string name)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossTraversalSystem>().TryStart(boss, BossTraversalKind.StepCross, Hit(1), direction: direction), Is.True);
            AdvanceTraversal(server, boss);
            AdvanceTraversal(server, boss);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            var origin = BossTraversalSystem.DirectionTile(direction) * 4;
            Assert.That(warning.PatternOrigin, Is.EqualTo(origin));
            var danger = warning.Danger.ToArray();
            AdvanceTraversal(server, boss);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That(effect.Comp.PatternOrigin, Is.EqualTo(origin));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
            Assert.That(effect.Comp.MovementCleave, Is.True);
            var style = server.ResolveDependency<IPrototypeManager>().Index(effect.Comp.Style);
            Assert.That(BossRiverVisuals.Texture(style, effect.Comp.Pattern, effect.Comp.PatternOrigin, effect.Comp.HalfSize)?.ToString(),
                Does.EndWith("cross_rivers_" + name + ".rsi"));
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(e => e.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
            AdvanceTraversal(server, boss);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(entities.EntityExists(effect.Owner), Is.True, "The return must not cut the river fade short.");
            Assert.That(effect.Comp.PatternOrigin, Is.EqualTo(origin));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
        });
    }

    [Test]
    public async Task RiverAnimationPausesAndExpiresAfterItsFullFade()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid effect = default;
        TimeSpan start = default;
        var tickRate = 0;
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var visuals = server.System<BossTelegraphSystem>();
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss,
                new BossTilePatternAttack { Telegraph = BossTelegraphPattern.Cross, Damage = Hit(1), CastTime = 5 }), Is.True);
            effect = visuals.ShowImpact(boss, visuals.SnapshotDanger(boss), playSound: false)!.Value;
            start = entities.GetComponent<BossTelegraphComponent>(effect).StartTime;
            tickRate = server.ResolveDependency<IGameTiming>().TickRate;
            server.System<SharedMapSystem>().SetPaused(entities.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(tickRate * 3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime, Is.EqualTo(2.2f));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That(server.EntMan.GetComponent<BossTelegraphComponent>(effect).StartTime, Is.GreaterThan(start));
        });
        await server.WaitRunTicks((int)Math.Floor(tickRate * 1.5));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(effect), Is.True);
            var visual = server.EntMan.GetComponent<BossTelegraphComponent>(effect);
            var elapsed = (server.ResolveDependency<IGameTiming>().CurTime - visual.StartTime).TotalSeconds;
            Assert.That(elapsed, Is.InRange(1.45, 1.55));
            Assert.That(server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime, Is.GreaterThan(.6f));
        });
        await server.WaitRunTicks(tickRate + 1);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(effect), Is.False));
    }
}
