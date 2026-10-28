using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(0, 0)] [TestCase(5, 5)] [TestCase(-5, -5)]
    public async Task SpreadSoulFreezesMovedTargetTileAndPreservesOriginalDamage(int x, int y)
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
                { Telegraph = BossTelegraphPattern.Spread, Damage = Hit(1), CastTime = 4 },
                selectedTargets: [players[1]]), Is.True);
            var marker = entities.AllEntities<BossActorMarkerComponent>().Single(e => e.Comp.Kind == BossActorMarkerKind.Spread);
            Assert.That(marker.Comp.StartTime, Is.EqualTo(server.ResolveDependency<IGameTiming>().CurTime));
            var center = new Vector2i(x, y);
            var expected = BossTilePatterns.Danger(BossTelegraphPattern.Spread, [center]);
            var safe = BossTilePatterns.Platform.First(tile => !expected.Contains(tile) && tile != center);
            var transform = server.System<SharedTransformSystem>();
            foreach (var player in players)
                transform.SetCoordinates(player, new EntityCoordinates(arena.Comp.CombatGrid, safe.X + .5f, safe.Y + .5f));
            transform.SetCoordinates(players[1], new EntityCoordinates(arena.Comp.CombatGrid, x + .5f, y + .5f));
            var victim = expected.First();
            transform.SetCoordinates(players[2], new EntityCoordinates(arena.Comp.CombatGrid, victim.X + .5f, victim.Y + .5f));
            Assert.That(visuals.SnapshotDanger(boss), Is.EquivalentTo(expected));
            var warning = visuals.GetTelegraph(boss)!.Value.Comp;
            Assert.That(warning.SpreadCenters, Is.EqualTo(new[] { center }));
            Assert.That(warning.SpreadTargets, Is.EqualTo(new[] { players[1] }));
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            entities.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That(effect.Comp.SpreadCenters, Is.EqualTo(new[] { center }));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(expected));
            Assert.That(effect.Comp.SpreadTargets, Is.EqualTo(new[] { players[1] }));
            Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds, Is.EqualTo(1).Within(.000001));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime, Is.EqualTo(1));
            Assert.That(entities.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.Zero, "An isolated source is safe in its own tile.");
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(), Is.EqualTo(1));
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(players[3]).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(e => e.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
            Assert.That(entities.EntityExists(marker.Owner), Is.False);
            transform.SetCoordinates(players[1], new EntityCoordinates(arena.Comp.CombatGrid, -3.5f, 3.5f));
            visuals.Update(0);
            Assert.That(effect.Comp.SpreadCenters, Is.EqualTo(new[] { center }), "A fading blast must not follow the player.");
            mechanics.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SpreadSoulKeepsCoincidentMarksAndTheirDangerousCentre()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var transform = server.System<SharedTransformSystem>();
            foreach (var player in players)
                transform.SetCoordinates(player, new EntityCoordinates(arena.Comp.CombatGrid, .5f, .5f));
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Spread, Damage = Hit(1), CastTime = 4 },
                selectedTargets: [players[1], players[2]]), Is.True);
            var visuals = server.System<BossTelegraphSystem>();
            Assert.That(visuals.SnapshotDanger(boss), Does.Contain(Vector2i.Zero));
            server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            var effect = server.EntMan.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact).Comp;
            Assert.That(effect.SpreadCenters, Is.EqualTo(new[] { Vector2i.Zero, Vector2i.Zero }));
            Assert.That(effect.SpreadTargets, Is.EqualTo(new[] { players[1], players[2] }));
            Assert.That(effect.Danger, Has.Count.EqualTo(25));
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.EqualTo(1));
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task SpreadSoulCancellationAndPreloadNeverEmitADamagingBurst(bool preload)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Spread, Damage = Hit(1), CastTime = 4 },
                preloadOnly: preload, selectedTargets: [players[1]]), Is.True);
            if (preload)
            {
                server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                mechanics.Update(0);
            }
            else mechanics.Cancel(boss);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>().Any(e => e.Comp.Impact), Is.False);
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }

    [Test]
    public async Task SpreadSoulMarkerAndCastClocksPauseTogether()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid marker = default;
        TimeSpan start = default, deadline = default;
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Spread, Damage = Hit(1), CastTime = 4 },
                selectedTargets: [players[1]]), Is.True);
            var mark = server.EntMan.AllEntities<BossActorMarkerComponent>().Single(e => e.Comp.Kind == BossActorMarkerKind.Spread);
            marker = mark.Owner; start = mark.Comp.StartTime;
            deadline = server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossActorMarkerComponent>(marker).StartTime, Is.EqualTo(start));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            var shift = server.EntMan.GetComponent<BossActorMarkerComponent>(marker).StartTime - start;
            Assert.That(shift.TotalSeconds, Is.GreaterThan(1));
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(arena.Comp.Boss).Deadline - deadline, Is.EqualTo(shift));
        });
    }

    [Test]
    public async Task SpreadSoulRemovesOnlyTheLegacySpreadCueAndRetainsApprovedGeometryAndFade()
    {
        var (server, _, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var style = server.ResolveDependency<IPrototypeManager>().Index<BossTelegraphStylePrototype>("RevenantBossTelegraphStyle");
            Assert.That(style.SpreadSoulTexture?.ToString(), Does.EndWith("spread_soul.rsi"));
            Assert.That(style.SpreadTexture, Is.Null);
            Assert.That(style.SpreadHighlightColor.A, Is.Zero);
            Assert.That(BossSpreadSoulVisuals.ReplacesLegacyCue(style, BossActorMarkerKind.Spread), Is.True);
            foreach (var kind in new[] { BossActorMarkerKind.Stack, BossActorMarkerKind.Tank, BossActorMarkerKind.TankBuster })
                Assert.That(BossSpreadSoulVisuals.ReplacesLegacyCue(style, kind), Is.False);
            var legacy = server.ResolveDependency<IPrototypeManager>().Index<BossTelegraphStylePrototype>("DefaultBossTelegraph");
            Assert.That(BossSpreadSoulVisuals.ReplacesLegacyCue(legacy, BossActorMarkerKind.Spread), Is.False);
            Assert.That(BossSpreadSoulVisuals.TileRegion(-2, 2), Is.EqualTo(new UIBox2(16, 16, 48, 48)));
            Assert.That(BossSpreadSoulVisuals.TileRegion(2, -2), Is.EqualTo(new UIBox2(144, 144, 176, 176)));
            Assert.That(BossSpreadSoulVisuals.Bounds(new Vector2i(192, 192)).Size, Is.EqualTo(new System.Numerics.Vector2(6, 6)));
            Assert.That(BossSpreadSoulVisuals.BurstFrame(.999), Is.EqualTo(24));
            Assert.That(BossSpreadSoulVisuals.BurstFrame(1), Is.EqualTo(25));
            Assert.That(BossSpreadSoulVisuals.Fade(.2), Is.GreaterThan(BossSpreadSoulVisuals.Fade(.8)));
            Assert.That(BossSpreadSoulVisuals.Fade(1), Is.Zero);
        });
    }
}
