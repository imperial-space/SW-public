using System.Net;
using Content.Server.Database;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossModuleTest
{
    [Test]
    public async Task GateWorksWithTheInstalledKeyRule()
    {
        var (server, arena, _) = await SetupArena();
        var sessions = await server.AddDummySessions(1);
        Task record = Task.CompletedTask;
        await server.WaitPost(() => record = server.ResolveDependency<IServerDbManager>()
            .UpdatePlayerRecordAsync(sessions[0].UserId, sessions[0].Name, IPAddress.Loopback, null));
        await record;
        EntityUid gate = default, user = default, key = default;
        var requiresKey = false;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            maps.CreateMap(out var mapId, runMapInit: true);
            var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            for (var y = -3; y <= 3; y++)
            for (var x = -3; x <= 3; x++)
                maps.SetTile(grid, grid.Comp, new Vector2i(x, y), new Tile(1));
            gate = entities.SpawnEntity("RevenantBossGate", new EntityCoordinates(grid, .5f, .5f));
            user = entities.SpawnEntity("MobHuman", new EntityCoordinates(grid, .5f, -.5f));
            server.PlayerMan.SetAttachedEntity(sessions[0], user);
            server.System<BossEncounterSystem>().End(arena.Comp.Boss);
            var gates = server.System<BossArenaGateSystem>();
            Assert.That(gates.LinkGate(gate, arena.Comp.Entrance), Is.True);
            var portal = entities.GetComponent<BossArenaGateComponent>(gate);
            portal.UnlockDuration = TimeSpan.FromSeconds(.1);
            portal.EntryDuration = TimeSpan.FromSeconds(.1);
            requiresKey = server.System<BossFeatureSystem>().Contains("arena-key");
            Assert.That(gates.TryUnlock(gate, user), Is.EqualTo(!requiresKey));
            if (!requiresKey) return;
            key = entities.SpawnEntity("RevenantBossGateKey", entities.GetComponent<TransformComponent>(user).Coordinates);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, key), Is.True);
            Assert.That(gates.TryUnlock(gate, user, key), Is.True);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(gate).Open, Is.True);
            if (requiresKey) Assert.That(server.EntMan.EntityExists(key), Is.False);
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, user), Is.True);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.GetComponent<TransformComponent>(user).GridUid, Is.EqualTo(arena.Comp.CombatGrid)));
    }

    [Test]
    public async Task SeparateGatesOwnSeparateArenaInstances()
    {
        var (server, arena, _) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var map = maps.CreateMap(out _, runMapInit: true);
            var entities = server.EntMan;
            var first = entities.SpawnEntity("RevenantBossGate", new EntityCoordinates(map, 0, 0));
            var second = entities.SpawnEntity("RevenantBossGate", new EntityCoordinates(map, 4, 0));
            var gates = server.System<BossArenaGateSystem>();
            Assert.That(gates.LinkGate(first, arena.Comp.Entrance), Is.True);
            Assert.That(gates.LinkGate(second, arena.Comp.Entrance), Is.True);
            var otherEntrance = entities.GetComponent<BossArenaGateComponent>(second).Destination!.Value;
            var otherMap = entities.GetComponent<TransformComponent>(otherEntrance).MapUid!.Value;
            Assert.That(otherMap, Is.Not.EqualTo(arena.Owner));
            Assert.That(entities.GetComponent<BossArenaMapComponent>(otherMap).EntryGate, Is.EqualTo(second));
            Assert.That(arena.Comp.EntryGate, Is.EqualTo(first));
        });
    }
}
