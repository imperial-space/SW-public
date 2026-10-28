using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using Content.Server.Database;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Interaction;
using Content.Shared.Storage.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossArenaGateTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task KeyRequiresTimeAndHoldingItThenEnablesConeAndOpeningEffect()
    {
        var (server, user, gate, entrance) = await SetupGate();
        var entities = server.EntMan;
        var gates = server.System<BossArenaGateSystem>();
        EntityUid key = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(gates.TryEnter(gate, user), Is.False);
            Assert.That(gates.SetOpen(gate, true), Is.False);
            Assert.That(gates.LinkGate(gate, entrance), Is.True);
            Assert.That(gates.TryUnlock(gate, user, user), Is.False);
            AssertLight(server, gate, false);
            key = entities.SpawnEntity("RevenantBossGateKey", entities.GetComponent<TransformComponent>(user).Coordinates);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, key), Is.True);
            Assert.That(gates.TryUnlock(gate, user, key), Is.True);
        });
        await RunSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.False);
            Assert.That(server.System<SharedHandsSystem>().TryDrop(user, key), Is.True);
        });
        await RunSeconds(server, 2.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.False, "Dropping the key cancels unlocking.");
            Assert.That(entities.EntityExists(key), Is.True, "Cancelled use must retain the key.");
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, key), Is.True);
            var interaction = new InteractUsingEvent(user, key, gate, entities.GetComponent<TransformComponent>(gate).Coordinates);
            entities.EventBus.RaiseLocalEvent(gate, interaction);
            Assert.That(interaction.Handled, Is.True);
        });
        await RunSeconds(server, 3.1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.True);
            Assert.That(entities.EntityExists(key), Is.False, "Successful opening consumes exactly its key.");
            AssertLight(server, gate, true);
            Assert.That(CountPrototype(server, "RevenantBossGateOpeningEffect"), Is.EqualTo(1));
            gates.SetOpen(gate, false);
            AssertLight(server, gate, false);
        });
        await RunSeconds(server, 0.6);
        await server.WaitAssertion(() => Assert.That(CountPrototype(server, "RevenantBossGateOpeningEffect"), Is.Zero));
    }

    [Test]
    public async Task WalkingNeverTeleportsAndClickWaitsFiveSecondsBeforeRotatedFrontArrival()
    {
        var (server, user, gate, entrance) = await SetupGate();
        var entities = server.EntMan;
        var gates = server.System<BossArenaGateSystem>();
        var transform = server.System<SharedTransformSystem>();
        EntityUid boss = default;
        await server.WaitAssertion(() =>
        {
            gates.LinkGate(gate, entrance);
            gates.SetOpen(gate, true);
            transform.SetLocalRotation(entrance, Angle.FromDegrees(90));
            var arena = entities.GetComponent<TransformComponent>(entrance).Coordinates.EntityId;
            boss = entities.SpawnEntity("RevenantBoss", new EntityCoordinates(arena, -2, 0));
            transform.SetCoordinates(user, entities.GetComponent<TransformComponent>(gate).Coordinates);
        });
        await RunSeconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            AssertSameMap(server, user, gate);
            var interaction = new InteractHandEvent(user, gate);
            entities.EventBus.RaiseLocalEvent(gate, interaction);
            Assert.That(interaction.Handled, Is.True);
            AssertSameMap(server, user, gate);
        });
        await RunSeconds(server, 4);
        await server.WaitAssertion(() => AssertSameMap(server, user, gate));
        await RunSeconds(server, 1.2);
        await server.WaitAssertion(() =>
        {
            AssertSameMap(server, user, entrance);
            var landed = entities.GetComponent<TransformComponent>(user).Coordinates.Position;
            var marker = entities.GetComponent<TransformComponent>(entrance).Coordinates.Position;
            Assert.That(landed.X, Is.EqualTo(marker.X + 1.5f).Within(0.01));
            Assert.That(landed.Y, Is.EqualTo(marker.Y).Within(0.01));
            Assert.That(entities.GetComponent<BossHealthScalingComponent>(boss).Locked, Is.False);
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.False);
        });
    }

    [TestCase("move")]
    [TestCase("click")]
    [TestCase("close")]
    [TestCase("relink")]
    [TestCase("delete")]
    public async Task EntryCancelsWithoutTeleporting(string reason)
    {
        var (server, user, gate, entrance) = await SetupGate();
        var gates = server.System<BossArenaGateSystem>();
        await server.WaitAssertion(() =>
        {
            gates.LinkGate(gate, entrance);
            gates.SetOpen(gate, true);
            Assert.That(gates.TryEnter(gate, user), Is.True);
        });
        await RunSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            switch (reason)
            {
                case "move":
                    var xform = server.EntMan.GetComponent<TransformComponent>(user);
                    server.System<SharedTransformSystem>().SetCoordinates(user, xform.Coordinates.Offset(new Vector2(1, 0)));
                    break;
                case "click":
                    Assert.That(gates.TryEnter(gate, user), Is.False);
                    break;
                case "close":
                    gates.SetOpen(gate, false);
                    gates.SetOpen(gate, true);
                    break;
                case "relink":
                    var replacement = server.EntMan.SpawnEntity("RevenantBossArenaEntrance", CreateGrid(server));
                    gates.LinkGate(gate, replacement);
                    break;
                case "delete":
                    server.EntMan.DeleteEntity(entrance);
                    Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(gate).Open, Is.False);
                    AssertLight(server, gate, false);
                    break;
            }
        });
        await RunSeconds(server, 4.2);
        await server.WaitAssertion(() => AssertSameMap(server, user, gate));
    }

    [Test]
    public async Task BlockedArrivalAndNonPlayersCannotEnter()
    {
        var (server, user, gate, entrance) = await SetupGate();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var gates = server.System<BossArenaGateSystem>();
            gates.LinkGate(gate, entrance);
            gates.SetOpen(gate, true);
            var npc = entities.SpawnEntity("MobHuman", entities.GetComponent<TransformComponent>(user).Coordinates);
            Assert.That(gates.TryEnter(gate, npc), Is.False);
            var arrival = entities.GetComponent<TransformComponent>(entrance).Coordinates.Offset(new Vector2(0, -1.5f));
            entities.SpawnEntity("RevenantBoss", arrival);
            Assert.That(gates.TryEnter(gate, user), Is.True, "One occupied landing must leave other front positions available.");
            gates.TryEnter(gate, user);
            foreach (var depth in new[] { 0f, 0.75f, 1.5f })
            foreach (var lateral in new[] { 0f, -0.9f, 0.9f, -1.8f, 1.8f, -2.7f, 2.7f, -3.6f, 3.6f })
                entities.SpawnEntity("RevenantBoss", arrival.Offset(new Vector2(lateral, -depth)));
            Assert.That(gates.TryEnter(gate, user), Is.False, "Fully blocked front landings must not fall back behind the marker.");
        });
    }

    [Test]
    public async Task DefeatLeavesOneCofferAndOtherRemovalDoesNot()
    {
        var (server, _, _, entrance) = await SetupGate();
        EntityUid boss = default;
        EntityCoordinates position = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            boss = entities.SpawnEntity("RevenantBoss", entities.GetComponent<TransformComponent>(entrance).Coordinates);
            position = entities.GetComponent<TransformComponent>(boss).Coordinates;
            var aliveBoss = entities.SpawnEntity("RevenantBoss", position);
            entities.DeleteEntity(aliveBoss);
            var lostBoss = entities.SpawnEntity("RevenantBoss", new EntityCoordinates(position.EntityId, 2, 2));
            var participant = entities.SpawnEntity("MobHuman", new EntityCoordinates(position.EntityId, 2, 1));
            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossfight([participant], lostBoss), Is.True);
            bosses.BossWon(lostBoss, entities.GetComponent<BossComponent>(lostBoss));
            server.System<BossSystem>().DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2);
            Assert.That(entities.GetComponent<BossComponent>(boss).Finished, Is.True);
            Assert.That(CountPrototype(server, "RevenantBossCoffer"), Is.Zero);
            server.System<BossSystem>().DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2);
        });
        await RunSeconds(server, 2.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(boss), Is.False);
            var coffers = server.EntMan.AllEntities<EntityStorageComponent>()
                .Where(ent => server.EntMan.GetComponent<MetaDataComponent>(ent.Owner).EntityPrototype?.ID == "RevenantBossCoffer").ToArray();
            Assert.That(coffers, Has.Length.EqualTo(1));
            Assert.That(server.EntMan.GetComponent<TransformComponent>(coffers[0].Owner).Coordinates, Is.EqualTo(position));
        });
        await RunSeconds(server, 1);
        await server.WaitAssertion(() => Assert.That(CountPrototype(server, "RevenantBossCoffer"), Is.EqualTo(1)));
    }

    private static int CountPrototype(ServerIntegrationInstance server, string prototype)
        => server.EntMan.AllEntities<MetaDataComponent>().Count(ent => ent.Comp.EntityPrototype?.ID == prototype);

    [Test]
    public async Task SimultaneousEntriesCompeteForLastSlotAndLeavingAllowsRetry()
    {
        var (server, first, gate, entrance) = await SetupGate();
        var sessions = await server.AddDummySessions(1);
        var record = Task.CompletedTask;
        await server.WaitPost(() => record = server.ResolveDependency<IServerDbManager>()
            .UpdatePlayerRecordAsync(sessions[0].UserId, sessions[0].Name, IPAddress.Loopback, null));
        await record;
        EntityUid second = default, resident = default, arenaMap = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var outside = entities.GetComponent<TransformComponent>(gate).ParentUid;
            second = entities.SpawnEntity("MobHuman", new EntityCoordinates(outside, -0.5f, -0.5f));
            server.PlayerMan.SetAttachedEntity(sessions[0], second);
            Assert.That(server.System<Robust.Shared.EntitySerialization.Systems.MapLoaderSystem>().TryLoadMap(
                new Robust.Shared.Utility.ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"), out var loaded, out _,
                new Robust.Shared.EntitySerialization.DeserializationOptions { InitializeMaps = true }), Is.True);
            arenaMap = loaded!.Value.Owner;
            server.System<SharedMapSystem>().SetPaused(loaded.Value.Comp.MapId, false);
            var arena = entities.GetComponent<BossArenaMapComponent>(arenaMap);
            var grid = arena.CombatGrid;
            entrance = arena.Entrance;
            for (var i = 0; i < 7; i++)
            {
                resident = entities.SpawnEntity("MobHuman", new EntityCoordinates(grid, 3.5f, 3.5f));
                entities.EnsureComponent<BossArenaPartyMemberComponent>(resident);
            }
            var gates = server.System<BossArenaGateSystem>();
            Assert.That(gates.LinkGate(gate, entrance), Is.True);
            Assert.That(gates.SetOpen(gate, true), Is.True);
            Assert.That(gates.TryEnter(gate, first), Is.True);
            Assert.That(gates.TryEnter(gate, second), Is.True);
        });
        await RunSeconds(server, 5.2);
        EntityUid waiting = default;
        await server.WaitAssertion(() =>
        {
            var firstInside = server.EntMan.GetComponent<TransformComponent>(first).MapUid == arenaMap;
            var secondInside = server.EntMan.GetComponent<TransformComponent>(second).MapUid == arenaMap;
            Assert.That(firstInside ^ secondInside, Is.True, "Only one channel may claim the eighth slot.");
            Assert.That(server.System<BossArenaMapSystem>().GetParty(arenaMap), Has.Count.EqualTo(8));
            waiting = firstInside ? second : first;
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, waiting), Is.False);
            var outside = server.EntMan.GetComponent<TransformComponent>(gate).ParentUid;
            server.System<SharedTransformSystem>().SetCoordinates(resident, new EntityCoordinates(outside, 3.5f, 3.5f));
            server.System<BossArenaMapSystem>().LeaveArena(resident, arenaMap);
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, waiting), Is.True);
        });
        await RunSeconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            AssertSameMap(server, waiting, entrance);
            Assert.That(server.System<BossArenaMapSystem>().GetParty(arenaMap), Has.Count.EqualTo(8));
        });
    }

    private static void AssertSameMap(ServerIntegrationInstance server, EntityUid first, EntityUid second)
        => Assert.That(server.EntMan.GetComponent<TransformComponent>(first).MapID,
            Is.EqualTo(server.EntMan.GetComponent<TransformComponent>(second).MapID));

    private static void AssertLight(ServerIntegrationInstance server, EntityUid gate, bool open)
    {
        Assert.That(server.EntMan.GetComponent<FixturesComponent>(gate).Fixtures["gate"].Hard, Is.True,
            "The gate must stay solid through unlocking, opening, closing and losing its destination.");
        var effects = server.EntMan.GetComponent<BossArenaGateEffectsComponent>(gate);
        Assert.That(server.EntMan.GetComponent<PointLightComponent>(effects.LeftFlameEntity!.Value).Enabled, Is.True);
        Assert.That(server.EntMan.GetComponent<PointLightComponent>(effects.RightFlameEntity!.Value).Enabled, Is.True);
        Assert.That(server.EntMan.GetComponent<PointLightComponent>(effects.ConeEntity!.Value).Enabled, Is.EqualTo(open));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FivePlayersCanEnterTogetherAndOneCancellationDoesNotBlockTheOthers(bool cancelOne)
    {
        var (server, first, gate, entrance) = await SetupGate();
        var sessions = await server.AddDummySessions(4);
        foreach (var session in sessions)
        {
            var record = Task.CompletedTask;
            await server.WaitPost(() => record = server.ResolveDependency<IServerDbManager>()
                .UpdatePlayerRecordAsync(session.UserId, session.Name, IPAddress.Loopback, null));
            await record;
        }
        var players = new List<EntityUid> { first };
        await server.WaitAssertion(() =>
        {
            var outside = server.EntMan.GetComponent<TransformComponent>(gate).ParentUid;
            foreach (var session in sessions)
            {
                var player = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(outside, 0, -0.5f));
                server.PlayerMan.SetAttachedEntity(session, player);
                players.Add(player);
            }
            var gatePosition = server.EntMan.GetComponent<TransformComponent>(gate).Coordinates.Position;
            Vector2[] positions = [new(-0.8f, -0.5f), new(0, -0.5f), new(0.8f, -0.5f),
                new(-0.4f, -0.9f), new(0.4f, -0.9f)];
            for (var i = 0; i < players.Count; i++)
                server.System<SharedTransformSystem>().SetCoordinates(players[i],
                    new EntityCoordinates(outside, gatePosition + positions[i]));
            var gates = server.System<BossArenaGateSystem>();
            gates.LinkGate(gate, entrance);
            gates.SetOpen(gate, true);
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                Assert.That(server.EntMan.HasComponent<ActorComponent>(player), Is.True, $"Player {i} remains attached.");
                Assert.That(server.System<SharedInteractionSystem>().InRangeUnobstructed(player, gate), Is.True,
                    $"Player {i} can reach the gate.");
                Assert.That(gates.TryEnter(gate, player), Is.True, $"Player {i} can start an independent channel.");
            }
            if (cancelOne) Assert.That(gates.TryEnter(gate, first), Is.False);
        });
        await RunSeconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            foreach (var player in players.Skip(cancelOne ? 1 : 0)) AssertSameMap(server, player, entrance);
            var positions = players.Skip(cancelOne ? 1 : 0)
                .Select(player => server.EntMan.GetComponent<TransformComponent>(player).Coordinates.Position).ToArray();
            Assert.That(positions.Distinct().Count(), Is.EqualTo(positions.Length));
            if (cancelOne)
            {
                AssertSameMap(server, first, gate);
                Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, first), Is.True);
            }
        });
        if (!cancelOne) return;
        await RunSeconds(server, 5.2);
        await server.WaitAssertion(() => AssertSameMap(server, first, entrance));
    }

    private async Task<(ServerIntegrationInstance Server, EntityUid User, EntityUid Gate, EntityUid Entrance)> SetupGate()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false, ContentStart = true, LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false }
        };
        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        var sessions = await server.AddDummySessions(1);
        var update = Task.CompletedTask;
        await server.WaitPost(() => update = server.ResolveDependency<IServerDbManager>().UpdatePlayerRecordAsync(
            sessions[0].UserId, sessions[0].Name, IPAddress.Loopback, null));
        await update;
        EntityUid user = default, gate = default, entrance = default;
        await server.WaitAssertion(() =>
        {
            var outside = CreateGrid(server);
            gate = server.EntMan.SpawnEntity("RevenantBossGate", outside);
            entrance = server.EntMan.SpawnEntity("RevenantBossArenaEntrance", CreateGrid(server));
            user = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(outside.EntityId, 0.5f, -0.5f));
            server.PlayerMan.SetAttachedEntity(sessions[0], user);
        });
        return (server, user, gate, entrance);
    }

    private static Task RunSeconds(ServerIntegrationInstance server, double seconds)
        => server.WaitRunTicks((int) Math.Ceiling(server.ResolveDependency<IGameTiming>().TickRate * seconds) + 1);

    private static EntityCoordinates CreateGrid(ServerIntegrationInstance server)
    {
        var maps = server.System<SharedMapSystem>();
        maps.CreateMap(out var mapId, runMapInit: true);
        var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
        for (var x = -4; x <= 4; x++)
        for (var y = -4; y <= 4; y++)
            maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
        return new EntityCoordinates(grid.Owner, 0, 0);
    }
}
