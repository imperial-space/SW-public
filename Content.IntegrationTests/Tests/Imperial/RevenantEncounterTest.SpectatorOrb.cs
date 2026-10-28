using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase("end")] [TestCase("idle")] [TestCase("victory")]
    [TestCase("reset")] [TestCase("boss-delete")] [TestCase("gate-delete")] [TestCase("map-delete")]
    public async Task SpectatorOrbExistsOnlyDuringCombatAndCleansItsCamera(string ending)
    {
        var (server, arena, players, gate) = await Setup();
        EntityUid orb = default, camera = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var system = server.System<BossArenaSpectatorSystem>();
            var portal = entities.GetComponent<BossArenaGateComponent>(gate);
            system.Refresh();
            Assert.That(portal.SpectatorOrb, Is.Null);
            server.System<BossSystem>().StartBossfight(players.ToList(), arena.Comp.Boss);
            system.Refresh();
            Assert.That(portal.SpectatorOrb, Is.Not.Null);
            orb = portal.SpectatorOrb!.Value;
            var spectator = entities.GetComponent<BossArenaSpectatorOrbComponent>(orb);
            camera = spectator.Camera;
            Assert.That(spectator.Arena, Is.EqualTo(arena.Owner));
            Assert.That(entities.GetComponent<TransformComponent>(camera).GridUid, Is.EqualTo(arena.Comp.CombatGrid));
            Assert.That(entities.GetComponent<TransformComponent>(camera).Coordinates.Position, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(entities.GetComponent<EyeComponent>(camera).DrawFov, Is.False);
            Assert.That(entities.GetComponent<EyeComponent>(camera).DrawLight, Is.True);
            var transform = server.System<SharedTransformSystem>();
            var expected = Vector2.Transform(portal.SpectatorOrbOffset, transform.GetWorldMatrix(gate));
            Assert.That(Vector2.Distance(transform.GetWorldPosition(orb), expected), Is.LessThan(.001));
            system.Refresh();
            Assert.That(portal.SpectatorOrb, Is.EqualTo(orb), "Do not duplicate the orb every update.");
            switch (ending)
            {
                case "end": server.System<BossEncounterSystem>().End(arena.Comp.Boss); break;
                case "idle": entities.GetComponent<BossEncounterComponent>(arena.Comp.Boss).IdleDeadline = server.ResolveDependency<IGameTiming>().CurTime; break;
                case "victory": server.System<BossSystem>().DamageBoss(arena.Comp.Boss, 100000); break;
                case "reset": Assert.That(server.System<BossEncounterSystem>().Reset(arena.Comp.Boss, arena), Is.True); break;
                case "boss-delete": entities.DeleteEntity(arena.Comp.Boss); break;
                case "gate-delete": entities.DeleteEntity(gate); break;
                case "map-delete": entities.DeleteEntity(arena.Owner); break;
            }
            system.Refresh();
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(orb), Is.False);
            Assert.That(server.EntMan.EntityExists(camera), Is.False);
        });
    }

    [Test]
    public async Task SpectatorOrbSupportsIndependentViewersWithoutMovingBodiesOrChangingTheirAttachment()
    {
        var (server, arena, players, gate) = await Setup();
        var sessions = await server.AddDummySessions(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            server.System<BossSystem>().StartBossfight(players.ToList(), arena.Comp.Boss);
            var spectators = server.System<BossArenaSpectatorSystem>();
            spectators.Refresh();
            var orb = entities.GetComponent<BossArenaGateComponent>(gate).SpectatorOrb!.Value;
            var spectator = entities.GetComponent<BossArenaSpectatorOrbComponent>(orb);
            var bodies = new EntityUid[2];
            var ui = server.System<UserInterfaceSystem>();
            for (var i = 0; i < 2; i++)
            {
                bodies[i] = entities.SpawnEntity("MobHuman", new EntityCoordinates(orb, 0, -.5f));
                server.PlayerMan.SetAttachedEntity(sessions[i], bodies[i]);
                var position = entities.GetComponent<TransformComponent>(bodies[i]).Coordinates;
                Assert.That(ui.TryOpenUi(orb, BossArenaSpectatorUiKey.Key, bodies[i]), Is.True);
                Assert.That(sessions[i].ViewSubscriptions, Does.Contain(spectator.Camera));
                Assert.That(sessions[i].AttachedEntity, Is.EqualTo(bodies[i]));
                Assert.That(entities.GetComponent<TransformComponent>(bodies[i]).Coordinates, Is.EqualTo(position));
                Assert.That(entities.HasComponent<BossArenaPartyMemberComponent>(bodies[i]), Is.False);
            }
            Assert.That(spectator.Viewers, Has.Count.EqualTo(2));
            ui.CloseUi(orb, BossArenaSpectatorUiKey.Key, bodies[0]);
            Assert.That(sessions[0].ViewSubscriptions, Does.Not.Contain(spectator.Camera));
            Assert.That(sessions[1].ViewSubscriptions, Does.Contain(spectator.Camera));
            var transform = server.System<SharedTransformSystem>();
            transform.SetCoordinates(bodies[1], new EntityCoordinates(entities.GetComponent<TransformComponent>(gate).ParentUid, -3, -3));
            spectators.Refresh();
            Assert.That(spectator.Viewers, Is.Empty);
            Assert.That(sessions[1].ViewSubscriptions, Does.Not.Contain(spectator.Camera));
            transform.SetCoordinates(bodies[1], new EntityCoordinates(orb, 0, -.5f));
            Assert.That(ui.TryOpenUi(orb, BossArenaSpectatorUiKey.Key, bodies[1]), Is.True);
            server.PlayerMan.SetAttachedEntity(sessions[1], null);
            spectators.Refresh();
            Assert.That(sessions[1].ViewSubscriptions, Does.Not.Contain(spectator.Camera));
            Assert.That(spectator.Viewers, Is.Empty);
            server.PlayerMan.SetAttachedEntity(sessions[0], null);
        });
    }

    [Test]
    public async Task SpectatorOrbRejectsRemoteArenaAccessAndReleasesSubscriptionsWhenFightEnds()
    {
        var (server, arena, players, gate) = await Setup();
        var sessions = await server.AddDummySessions(1);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            server.System<BossSystem>().StartBossfight(players.ToList(), arena.Comp.Boss);
            var spectators = server.System<BossArenaSpectatorSystem>();
            spectators.Refresh();
            var orb = entities.GetComponent<BossArenaGateComponent>(gate).SpectatorOrb!.Value;
            var spectator = entities.GetComponent<BossArenaSpectatorOrbComponent>(orb);
            var ui = server.System<UserInterfaceSystem>();
            server.PlayerMan.SetAttachedEntity(sessions[0], players[0]);
            Assert.That(ui.TryOpenUi(orb, BossArenaSpectatorUiKey.Key, players[0]), Is.False);
            Assert.That(sessions[0].ViewSubscriptions, Does.Not.Contain(spectator.Camera));
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(orb, 0, -.5f));
            Assert.That(ui.TryOpenUi(orb, BossArenaSpectatorUiKey.Key, players[0]), Is.True);
            Assert.That(sessions[0].ViewSubscriptions, Does.Contain(spectator.Camera));
            server.System<BossEncounterSystem>().End(arena.Comp.Boss);
            spectators.Refresh();
            Assert.That(sessions[0].ViewSubscriptions, Does.Not.Contain(spectator.Camera));
            Assert.That(ui.GetActors(orb, BossArenaSpectatorUiKey.Key), Is.Empty);
            server.PlayerMan.SetAttachedEntity(sessions[0], null);
        });
    }

    [Test]
    public async Task SpectatorOrbUsesOneCameraPerGateAndDoesNotSpawnOnUnconfiguredGates()
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var second = entities.SpawnEntity("RevenantBossGate", new EntityCoordinates(gate, -3, 0));
            Assert.That(server.System<BossArenaGateSystem>().LinkGate(second, arena.Comp.Entrance), Is.True);
            var secondPortal = entities.GetComponent<BossArenaGateComponent>(second);
            Assert.That(secondPortal.Destination,Is.Not.EqualTo(arena.Comp.Entrance));
            var secondMap=entities.GetComponent<TransformComponent>(secondPortal.Destination!.Value).MapUid!.Value;
            var secondArena=entities.GetComponent<BossArenaMapComponent>(secondMap);
            entities.DeleteEntity(secondArena.Crystal!.Value);
            secondArena.Crystal=null;
            secondArena.Boss=entities.SpawnEntity("RevenantBoss",new EntityCoordinates(secondArena.CombatGrid,.5f,.5f));
            var secondPlayer=entities.SpawnEntity("MobHuman",new EntityCoordinates(secondArena.CombatGrid,1.5f,.5f));
            entities.EnsureComponent<BossArenaPartyMemberComponent>(secondPlayer);
            Assert.That(server.System<BossSystem>().TryStartBossfight(new(){secondPlayer},secondArena.Boss),Is.True);
            server.System<BossSystem>().StartBossfight(players.ToList(), arena.Comp.Boss);
            var spectators = server.System<BossArenaSpectatorSystem>();
            spectators.Refresh();
            var firstOrb = entities.GetComponent<BossArenaGateComponent>(gate).SpectatorOrb!.Value;
            var secondOrb = secondPortal.SpectatorOrb!.Value;
            Assert.That(entities.GetComponent<BossArenaSpectatorOrbComponent>(secondOrb).Arena,Is.EqualTo(secondMap));
            Assert.That(entities.GetComponent<BossArenaSpectatorOrbComponent>(firstOrb).Camera,
                Is.Not.EqualTo(entities.GetComponent<BossArenaSpectatorOrbComponent>(secondOrb).Camera));
            secondPortal.SpectatorOrbPrototype = null;
            spectators.Refresh();
            Assert.That(secondPortal.SpectatorOrb, Is.Null);
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).SpectatorOrb, Is.EqualTo(firstOrb));
        });
    }
}
