using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossModuleTest
{
    [Test]
    public async Task SpectatorOrbWorksWithoutAGateAndKeepsBothViewers()
    {
        var (server, arena, _) = await SetupArena();
        var sessions = await server.AddDummySessions(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var spectators = server.System<BossArenaSpectatorSystem>();
            spectators.Refresh();
            var anchor = entities.GetComponent<BossArenaSpectatorAnchorComponent>(arena.Comp.Entrance);
            Assert.That(anchor.Orb, Is.Not.Null);
            var orb = anchor.Orb!.Value;
            var spectator = entities.GetComponent<BossArenaSpectatorOrbComponent>(orb);
            var ui = server.System<UserInterfaceSystem>();
            var bodies = new EntityUid[2];
            for (var i = 0; i < 2; i++)
            {
                bodies[i] = entities.SpawnEntity("MobHuman", new EntityCoordinates(orb, 0, -.5f));
                server.PlayerMan.SetAttachedEntity(sessions[i], bodies[i]);
                Assert.That(ui.TryOpenUi(orb, BossArenaSpectatorUiKey.Key, bodies[i]), Is.True);
                Assert.That(sessions[i].ViewSubscriptions, Does.Contain(spectator.Camera));
                Assert.That(sessions[i].AttachedEntity, Is.EqualTo(bodies[i]));
            }
            Assert.That(spectator.Viewers, Has.Count.EqualTo(2));
            ui.CloseUi(orb, BossArenaSpectatorUiKey.Key, bodies[0]);
            Assert.That(sessions[0].ViewSubscriptions, Does.Not.Contain(spectator.Camera));
            Assert.That(sessions[1].ViewSubscriptions, Does.Contain(spectator.Camera));
            server.System<BossEncounterSystem>().End(arena.Comp.Boss);
            spectators.Refresh();
            Assert.That(sessions[1].ViewSubscriptions, Does.Not.Contain(spectator.Camera));
            foreach (var session in sessions) server.PlayerMan.SetAttachedEntity(session, null);
        });
    }
}
