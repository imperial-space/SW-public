using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    private static EntityUid ResolveAutoContact(ServerIntegrationInstance server, EntityUid boss)
    {
        var entities = server.EntMan;
        var effect = entities.AllEntities<BossAutoProjectileComponent>().Last(entity =>
            entity.Comp.Boss == boss && !entity.Comp.Resolved && !entities.IsQueuedForDeletion(entity.Owner));
        var transform = server.System<SharedTransformSystem>();
        effect.Comp.Position = Vector2.Transform(transform.GetWorldPosition(effect.Comp.Target), transform.GetInvWorldMatrix(effect.Comp.Grid));
        effect.Comp.LaunchTime = effect.Comp.PositionTime = server.ResolveDependency<IGameTiming>().CurTime;
        server.System<BossAutoProjectileSystem>().Update(0);
        Assert.That(effect.Comp.Resolved, Is.True);
        return effect.Owner;
    }

    private static EntityUid StartTestAuto(ServerIntegrationInstance server, Entity<BossArenaMapComponent> arena, EntityUid[] players)
    {
        var boss = arena.Comp.Boss;
        var encounter = server.EntMan.GetComponent<BossEncounterComponent>(boss);
        encounter.OpeningDelay = encounter.TankInterval = 1000;
        encounter.TankDamage = Hit(18);
        server.System<BossSystem>().StartBossfight(players.ToList(), boss);
        encounter.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
        server.System<BossEncounterSystem>().Update(0);
        return server.EntMan.AllEntities<BossAutoProjectileComponent>().Single(entity => entity.Comp.Boss == boss).Owner;
    }

    [Test]
    public async Task AutoProjectileFormsAtBossSpriteCenterAndFollowsItsBobBeforeLaunch()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid effect = default;
        await server.WaitAssertion(() =>
        {
            var transform = server.System<SharedTransformSystem>();
            transform.SetCoordinates(arena.Comp.Boss, new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 2.5f));
            effect = StartTestAuto(server, arena, players);
            var shot = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect);
            Assert.That(shot.HandOffset, Is.EqualTo(new Vector2(0, .5f)));
        });
        await server.WaitRunTicks(7);
        await server.WaitAssertion(() =>
        {
            var transform = server.System<SharedTransformSystem>();
            var boss = arena.Comp.Boss;

            transform.SetCoordinates(boss, new EntityCoordinates(arena.Comp.CombatGrid, -1.5f, 1.5f));
            server.System<BossAutoProjectileSystem>().Update(0);
            var shot = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(now, Is.LessThan(shot.LaunchTime));
            var presentation = server.EntMan.GetComponent<BossPresentationComponent>(boss);
            var expected = transform.GetWorldPosition(boss) + new Vector2(0,
                .5f + BossPresentationComponent.BobOffset(now * presentation.BobSpeedMultiplier));
            var actual = Vector2.Transform(shot.Position, transform.GetWorldMatrix(shot.Grid));
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.00001f),
                "The forming flame must be centered on the displayed boss, without an eastward muzzle offset.");
            Assert.That(shot.PredictedPosition(now, shot.Position + Vector2.UnitX * 4), Is.EqualTo(shot.Position));
        });
    }

    [TestCase(-4.5f)] [TestCase(4.5f)]
    public async Task AutoProjectileFollowsMovingEdgeTankAndDamagesExactlyOnceOnContact(float edge)
    {
        var (server, arena, players, _) = await Setup();
        EntityUid effect = default;
        Vector2 initial = default;
        await server.WaitAssertion(() =>
        {
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, edge, 4.5f));
            effect = StartTestAuto(server, arena, players);
            var shot = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect);
            initial = shot.Position;
            Assert.That(shot.Target, Is.EqualTo(players[0]));
            Assert.That(shot.Speed, Is.EqualTo(10));
            Assert.That(shot.Scale, Is.EqualTo(.5));
            Assert.That(shot.AnchorPixels, Is.EqualTo(new Vector2(120, 80)));
            Assert.That((shot.LaunchTime - shot.StartTime).TotalSeconds, Is.EqualTo(.34).Within(.000001));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            Assert.That(server.EntMan.HasComponent<Robust.Shared.Physics.Components.PhysicsComponent>(effect), Is.False);
        });
        await server.WaitRunTicks(7);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossAutoProjectileComponent>(effect).Resolved, Is.False);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            var shot = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect);
            Assert.That(shot.Resolved, Is.False);
            Assert.That(Vector2.Distance(shot.Position, initial), Is.GreaterThan(.5));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);

            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, -edge, 4.5f));
        });
        var resolved = false;
        for (var i = 0; i < 30 && !resolved; i++)
        {
            await server.WaitRunTicks(5);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.EntityExists(effect), Is.True);
                resolved = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect).Resolved;
            });
        }
        await server.WaitAssertion(() =>
        {
            Assert.That(resolved, Is.True);
            var shot = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect);
            var transform = server.System<SharedTransformSystem>();
            var target = Vector2.Transform(transform.GetWorldPosition(players[0]), transform.GetInvWorldMatrix(shot.Grid));
            Assert.That(shot.Position, Is.EqualTo(target));
            Assert.That((shot.EndTime - shot.ImpactTime).TotalSeconds, Is.EqualTo(.5).Within(.000001));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Slash"].Float(), Is.EqualTo(18));
            foreach (var other in players.Skip(1))
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(other).TotalDamage.Float(), Is.Zero);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>().Any(entity => entity.Comp.TankTarget == players[0]), Is.False);
            var impact = shot.Position;
            transform.SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, .5f, -3.5f));
            server.System<BossAutoProjectileSystem>().Update(0);
            Assert.That(shot.Position, Is.EqualTo(impact), "The contact burst stays where the shot landed.");
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).Damage.DamageDict["Slash"].Float(), Is.EqualTo(18));
            shot.EndTime = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossAutoProjectileSystem>().Update(0);
            Assert.That(server.EntMan.IsQueuedForDeletion(effect), Is.True);
        });
    }

    [Test]
    public async Task AutoProjectileKeepsItsSelectedTankWhenFocusChangesAndCanHitCriticalTank()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var effect = StartTestAuto(server, arena, players);
            server.System<BossFocusMarkerSystem>().SetTank(arena.Comp.Boss, players[1]);
            server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Critical);
            Assert.That(server.EntMan.GetComponent<BossAutoProjectileComponent>(effect).Target, Is.EqualTo(players[0]));
            ResolveAutoContact(server, arena.Comp.Boss);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(18));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.Zero);
        });
    }

    [TestCase("target-leaves")] [TestCase("target-dies")] [TestCase("target-deletion")]
    [TestCase("end")] [TestCase("victory")] [TestCase("generation")] [TestCase("boss-deletion")] [TestCase("map-deletion")]
    public async Task AutoProjectileCannotHitAfterTargetOrEncounterBecomesInvalid(string reason)
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var effect = StartTestAuto(server, arena, players);
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            switch (reason)
            {
                case "target-leaves": server.System<SharedTransformSystem>().SetCoordinates(players[0], entities.GetComponent<TransformComponent>(gate).Coordinates); break;
                case "target-dies": server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Dead); break;
                case "target-deletion": entities.DeleteEntity(players[0]); break;
                case "end": server.System<BossEncounterSystem>().End(boss); break;
                case "victory": server.System<BossSystem>().DamageBoss(boss, 100000); break;
                case "generation": entities.GetComponent<BossEncounterComponent>(boss).Generation++; break;
                case "boss-deletion": entities.DeleteEntity(boss); break;
                case "map-deletion": entities.DeleteEntity(arena.Owner); break;
            }
            server.System<BossAutoProjectileSystem>().Update(0);
            Assert.That(!entities.EntityExists(effect) || entities.IsQueuedForDeletion(effect), Is.True);
            foreach (var player in players.Where(entities.EntityExists))
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }

    [Test]
    public async Task AutoProjectileAndItsLaunchClockPauseWithTheArena()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid effect = default;
        TimeSpan launch = default, start = default, positionTime = default;
        Vector2 position = default;
        await server.WaitAssertion(() =>
        {
            effect = StartTestAuto(server, arena, players);
            var shot = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect);
            launch = shot.LaunchTime; start = shot.StartTime; positionTime = shot.PositionTime; position = shot.Position;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            var shot = server.EntMan.GetComponent<BossAutoProjectileComponent>(effect);
            Assert.That(shot.LaunchTime, Is.EqualTo(launch));
            Assert.That(shot.Position, Is.EqualTo(position));
            Assert.That(shot.Resolved, Is.False);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            var shift = shot.LaunchTime - launch;
            Assert.That(shift.TotalSeconds, Is.GreaterThan(1));
            Assert.That(shot.StartTime - start, Is.EqualTo(shift));
            Assert.That(shot.PositionTime - positionTime, Is.EqualTo(shift));
        });
    }

    [Test]
    public async Task AutoProjectileOptOutPreservesLegacyImmediateTankStrike()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.EntMan.GetComponent<BossTelegraphSettingsComponent>(boss).AutoProjectilePrototype = null;
            var encounter = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            encounter.OpeningDelay = encounter.TankInterval = 1000;
            encounter.TankDamage = Hit(18);
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            encounter.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(server.EntMan.AllEntities<BossAutoProjectileComponent>(), Is.Empty);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(18));
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>().Count(entity => entity.Comp.Impact && entity.Comp.TankTarget == players[0]), Is.EqualTo(1));
        });
    }

    [Test]
    public void AutoProjectilePredictionDoesNotPassTargetAndBurstFadesAtApprovedRate()
    {
        var shot = new BossAutoProjectileComponent
        {
            Position = Vector2.Zero, PositionTime = TimeSpan.Zero, LaunchTime = TimeSpan.Zero, Speed = 6.25f
        };
        Assert.That(shot.PredictedPosition(TimeSpan.FromSeconds(.1), new Vector2(1, 0)), Is.EqualTo(new Vector2(.625f, 0)));
        Assert.That(shot.PredictedPosition(TimeSpan.FromSeconds(.2), new Vector2(1, 0)), Is.EqualTo(new Vector2(1, 0)));
        Assert.That(shot.PredictedPosition(TimeSpan.FromSeconds(2), new Vector2(4, 0)), Is.EqualTo(new Vector2(1.25f, 0)));
        shot.Resolved = true;
        Assert.That(shot.PredictedPosition(TimeSpan.FromSeconds(1), new Vector2(4, 0)), Is.EqualTo(Vector2.Zero));
        Assert.That(BossAutoProjectileVisuals.FlightLoopSeconds, Is.EqualTo(1.2));
        Assert.That(BossAutoProjectileVisuals.ImpactOpacity(.05), Is.EqualTo(1));
        Assert.That(BossAutoProjectileVisuals.ImpactOpacity(.2), Is.GreaterThan(BossAutoProjectileVisuals.ImpactOpacity(.4)));
        Assert.That(BossAutoProjectileVisuals.ImpactOpacity(.5), Is.Zero);
    }

    [TestCase(-3f, 0f)] [TestCase(0f, 3f)] [TestCase(0f, -3f)] [TestCase(-3f, -3f)]
    public void AutoFormationFacesTheTankBeforeLaunchAndKeepsItsAnimationClock(float x, float y)
    {
        var shot = new BossAutoProjectileComponent { Position = new Vector2(1, 2), Direction = Vector2.UnitX };
        var target = shot.Position + new Vector2(x, y);
        Assert.That(shot.AimDirection(target), Is.EqualTo(Vector2.Normalize(target - shot.Position)));
        Assert.That(shot.AimDirection(shot.Position), Is.EqualTo(Vector2.UnitX), "Contact retains the last valid direction.");
        Assert.That(shot.Speed, Is.EqualTo(10));
        Assert.That(BossAutoProjectileVisuals.FlightFrameSeconds, Is.EqualTo(.1));
        Assert.That(BossAutoProjectileVisuals.FlightLoopSeconds, Is.EqualTo(1.2));
    }

    [Test]
    public void AutoBirthStartsAtFirstCelAndLaunchPreservesPhaseWithoutEarlyMotion()
    {
        Assert.That(BossAutoProjectileVisuals.FlameTime(.12), Is.EqualTo(TimeSpan.Zero));
        Assert.That(BossAutoProjectileVisuals.BirthOpacity(.12), Is.Zero);
        Assert.That(BossAutoProjectileVisuals.BirthOpacity(.14), Is.EqualTo(.5).Within(.000001));
        Assert.That(BossAutoProjectileVisuals.BirthOpacity(.16), Is.EqualTo(1));
        Assert.That(BossAutoProjectileVisuals.FormationProgress(.34), Is.EqualTo(1));
        Assert.That(BossAutoProjectileVisuals.FlameTime(.34).TotalSeconds, Is.EqualTo(.22).Within(.000001));
        Assert.That((BossAutoProjectileVisuals.FlameTime(.341) - BossAutoProjectileVisuals.FlameTime(.339)).TotalSeconds,
            Is.EqualTo(.002).Within(.000001), "Formation and flight must share a continuous clock.");
        var shot = new BossAutoProjectileComponent { PositionTime = TimeSpan.FromSeconds(.32), LaunchTime = TimeSpan.FromSeconds(.34) };
        Assert.That(shot.PredictedPosition(shot.LaunchTime, new Vector2(4, 0)), Is.EqualTo(Vector2.Zero));
        Assert.That(shot.PredictedPosition(TimeSpan.FromSeconds(.35), new Vector2(4, 0)).X, Is.EqualTo(.1f).Within(.000001));
    }
}
