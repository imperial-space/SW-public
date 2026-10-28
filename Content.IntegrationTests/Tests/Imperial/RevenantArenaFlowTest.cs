using System.Linq;
using System.Net;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Database;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Physics;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed partial class RevenantArenaFlowTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task KeyCreatesArenaOnlyOnCompletionReusesItAndRecreatesDeletedMap()
    {
        var (server, user, gate, key) = await SetupOutside();
        var entities = server.EntMan;
        var gates = server.System<BossArenaGateSystem>();
        EntityUid arenaMap = default, boss = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.AllEntities<BossArenaMapComponent>(), Is.Empty);
            Assert.That(gates.TryUnlock(gate, user, key), Is.True);
        });
        await RunSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.AllEntities<BossArenaMapComponent>(), Is.Empty);
            server.System<SharedHandsSystem>().TryDrop(user, key);
        });
        await RunSeconds(server, 2.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.AllEntities<BossArenaMapComponent>(), Is.Empty, "Cancelled key use cannot create a map.");
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, key), Is.True);
            Assert.That(gates.TryUnlock(gate, user, key), Is.True);
        });
        await RunSeconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            var arena = entities.AllEntities<BossArenaMapComponent>().Single();
            arenaMap = arena.Owner;
            boss = arena.Comp.Boss;
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Destination, Is.EqualTo(arena.Comp.Entrance));
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.True);
            Assert.That(server.System<SharedMapSystem>().IsPaused(entities.GetComponent<TransformComponent>(arenaMap).MapID), Is.False);
            Assert.That(entities.EntityExists(key), Is.False);
            gates.SetOpen(gate, false);
            key = GiveKey(server, user);
            Assert.That(gates.TryUnlock(gate, user, key), Is.True);
        });
        await RunSeconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            var arena = entities.AllEntities<BossArenaMapComponent>().Single();
            Assert.That(arena.Owner, Is.EqualTo(arenaMap));
            Assert.That(arena.Comp.Boss, Is.EqualTo(boss));
            server.System<SharedMapSystem>().DeleteMap(entities.GetComponent<TransformComponent>(arenaMap).MapID);
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Destination, Is.Null);
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.False);
            Assert.That(entities.EntityExists(key), Is.False);
            key = GiveKey(server, user);
            Assert.That(gates.TryUnlock(gate, user, key), Is.True);
        });
        await RunSeconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            var arena = entities.AllEntities<BossArenaMapComponent>().Single();
            Assert.That(arena.Owner, Is.Not.EqualTo(arenaMap));
            Assert.That(entities.AllEntities<BossComponent>(), Is.Empty);
            Assert.That(entities.AllEntities<BossSummoningTriggerComponent>(), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task KeyReusesManuallyLoadedArena()
    {
        var (server, user, gate, key) = await SetupOutside();
        EntityUid mapUid = default, secondGate = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"), out var map, out _,
                new DeserializationOptions { InitializeMaps = true }), Is.True);
            mapUid = map!.Value.Owner;
            Assert.That(server.System<BossArenaGateSystem>().TryUnlock(gate, user, key), Is.True);
        });
        await RunSeconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            var arena = server.EntMan.AllEntities<BossArenaMapComponent>().Single();
            Assert.That(arena.Owner, Is.EqualTo(mapUid));
            Assert.That(arena.Comp.EntryGate, Is.EqualTo(gate));
            Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(gate).Open, Is.True);
            var grid = server.EntMan.GetComponent<TransformComponent>(gate).GridUid!.Value;
            secondGate = server.EntMan.SpawnEntity("RevenantBossGate", new EntityCoordinates(grid, 2.5f, 0.5f));
            server.System<SharedTransformSystem>().SetCoordinates(user, new EntityCoordinates(grid, 2.5f, -0.5f));
            Assert.That(server.EntMan.EntityExists(key), Is.False);
            key = GiveKey(server, user);
            Assert.That(server.System<BossArenaGateSystem>().TryUnlock(secondGate, user, key), Is.True);
        });
        await RunSeconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            var arenas = server.EntMan.AllEntities<BossArenaMapComponent>();
            Assert.That(arenas, Has.Length.EqualTo(2));
            var arena = arenas.Single(entry => entry.Owner != mapUid);
            Assert.That(arena.Comp.EntryGate, Is.EqualTo(secondGate));
            Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(gate).Destination,
                Is.EqualTo(server.EntMan.GetComponent<BossArenaMapComponent>(mapUid).Entrance));
            Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(secondGate).Destination, Is.EqualTo(arena.Comp.Entrance));
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(secondGate, user), Is.True);
        });
        await RunSeconds(server, 5.2);
        await RunSeconds(server, 1.1);
        await server.WaitAssertion(() =>
        {
            var arena = server.EntMan.AllEntities<BossArenaMapComponent>()
                .Single(entry => entry.Owner == server.EntMan.GetComponent<TransformComponent>(user).MapUid);
            Assert.That(server.EntMan.GetComponent<BossArenaVisitorComponent>(user).Gate, Is.EqualTo(secondGate));
            var crystal = arena.Comp.Crystal!.Value;
            var summon = server.EntMan.GetComponent<BossSummoningTriggerComponent>(crystal);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(summon.Health);
            server.System<DamageableSystem>().TryChangeDamage(crystal, damage, origin: user);
            Assert.That(server.EntMan.EntityExists(arena.Comp.Boss), Is.True, "Breaking the crystal must summon the boss in the second gate's own arena.");
            server.System<BossSystem>().DamageBoss(arena.Comp.Boss, server.EntMan.GetComponent<BossComponent>(arena.Comp.Boss).MaxHealth * 2);
            var returnPosition = server.EntMan.GetComponent<TransformComponent>(arena.Comp.VictoryExit!.Value).Coordinates;
            server.System<SharedTransformSystem>().SetCoordinates(user, returnPosition.Offset(new Vector2(0, -1)));
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(arena.Comp.VictoryExit!.Value, user), Is.True);
        });
        await RunSeconds(server, 5.2);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<TransformComponent>(user).Coordinates.Position,
            Is.EqualTo(new Vector2(2.5f, -1)), "Return must use the player's second gate, not the arena's first gate."));
    }

    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public async Task ChasmLeavesDeadBodyAtArenaEntranceWithEquipment(int rotation)
    {
        var (server,user,gate,key)=await SetupOutside();
        await OpenAndEnter(server,user,gate,key);
        EntityUid equipment=default;
        await server.WaitAssertion(()=>
        {
            server.System<SharedTransformSystem>().SetLocalRotation(gate,Angle.FromDegrees(rotation));
            equipment=GiveKey(server,user);
        });
        var entities=server.EntMan;
        var arena=entities.AllEntities<BossArenaMapComponent>().Single();
        await server.WaitAssertion(()=>
        {
            server.System<SharedTransformSystem>().SetCoordinates(user,new EntityCoordinates(arena.Owner,100,100));
            Assert.That(server.System<BossArenaMapSystem>().TryReturnFromPit(user,arena.Owner,arena.Comp),Is.True);
        });
        await RunSeconds(server,1.6);
        await server.WaitAssertion(()=>Assert.That(entities.HasComponent<BossArenaFallingComponent>(user),Is.True));
        await RunSeconds(server,.5);
        await server.WaitAssertion(()=>
        {
            AssertDeadAtEntrance(server,user,arena);
            Assert.That(entities.EntityExists(equipment),Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(equipment).MapUid,Is.EqualTo(arena.Owner),
                "Equipment returns with the corpse, including items dropped by death.");
        });
        await RunSeconds(server,2.1);
        await server.WaitAssertion(()=>Assert.That(entities.EntityExists(user),Is.True));
    }

    [TestCase("Blunt")]
    [TestCase("Slash")]
    public async Task PitReturnKillsInjuredBodyWithoutLosingExistingDamage(string injuryType)
    {
        var (server,user,gate,key)=await SetupOutside();await OpenAndEnter(server,user,gate,key);
        var e=server.EntMan;var arena=e.AllEntities<BossArenaMapComponent>().Single();
        await server.WaitAssertion(()=>
        {
            e.RemoveComponent<PassiveDamageComponent>(user);
            server.System<DamageableSystem>().SetDamage(user,e.GetComponent<DamageableComponent>(user),
                new DamageSpecifier{DamageDict={[injuryType]=FixedPoint2.New(97)}});
            server.System<SharedTransformSystem>().SetCoordinates(user,new EntityCoordinates(arena.Owner,100,100));
            Assert.That(server.System<BossArenaMapSystem>().TryReturnFromPit(user,arena.Owner,arena.Comp),Is.True);
        });
        await RunSeconds(server,2.1);
        await server.WaitAssertion(()=>
        {
            AssertDeadAtEntrance(server,user,arena);
            Assert.That(e.GetComponent<DamageableComponent>(user).Damage.DamageDict[injuryType].Float(),Is.GreaterThanOrEqualTo(97));
        });
    }

    [Test]
    public async Task VictoryCreatesOneExitAndCancellableChannelReturnsWithoutPitDamage()
    {
        var (server, user, gate, key) = await SetupOutside();
        await OpenAndEnter(server, user, gate, key);
        var entities = server.EntMan;
        var arena = entities.AllEntities<BossArenaMapComponent>().Single();
        EntityUid exit = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(arena.Comp.VictoryExit, Is.Null);
            server.System<BossSystem>().DamageBoss(arena.Comp.Boss, server.EntMan.GetComponent<BossComponent>(arena.Comp.Boss).MaxHealth * 2);
            exit = arena.Comp.VictoryExit!.Value;
            var returnPosition = entities.GetComponent<TransformComponent>(exit).Coordinates;
            Assert.That(returnPosition.EntityId, Is.EqualTo(arena.Comp.CombatGrid));
            Assert.That(returnPosition.Position, Is.EqualTo(new Vector2(0.5f, 4.5f)));
            server.System<SharedTransformSystem>().SetCoordinates(user, returnPosition.Offset(new Vector2(0, -1)));
            Assert.That(entities.GetComponent<BossArenaGateComponent>(exit).ReturnToGate, Is.True);
            server.System<BossSystem>().DamageBoss(arena.Comp.Boss, server.EntMan.GetComponent<BossComponent>(arena.Comp.Boss).MaxHealth * 2);
            Assert.That(entities.AllEntities<BossArenaGateComponent>().Count(ent => ent.Comp.ReturnToGate), Is.EqualTo(1));
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(exit, user), Is.True);
        });
        await RunSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            var xform = entities.GetComponent<TransformComponent>(user);
            server.System<SharedTransformSystem>().SetCoordinates(user, xform.Coordinates.Offset(new Vector2(1, 0)));
        });
        await RunSeconds(server, 4.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<TransformComponent>(user).MapUid, Is.EqualTo(arena.Owner));
            Assert.That(entities.AllEntities<MetaDataComponent>().Count(ent => ent.Comp.EntityPrototype?.ID == "RevenantBossCoffer"), Is.EqualTo(1));
            var chest = entities.AllEntities<BossEncounterRewardComponent>().Single().Owner;
            var storage = entities.GetComponent<EntityStorageComponent>(chest);
            var fixture = entities.GetComponent<FixturesComponent>(chest).Fixtures.Values.Single();
            Assert.That(fixture.CollisionLayer & (int) CollisionGroup.MobMask, Is.Not.Zero);
            server.System<SharedEntityStorageSystem>().OpenStorage(chest);
            Assert.That(storage.Open, Is.True);
            Assert.That(fixture.CollisionLayer & (int) CollisionGroup.MobMask, Is.Zero,
                "Native chest opening must allow players to walk over the coffer.");
            server.System<SharedEntityStorageSystem>().CloseStorage(chest);
            Assert.That(fixture.CollisionLayer & (int) CollisionGroup.MobMask, Is.Not.Zero);
            server.System<BossArenaGateSystem>().SetOpen(gate, false);
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(exit, user), Is.True,
                "Returning does not depend on the outside gate remaining open.");
        });
        await RunSeconds(server, 4);
        await server.WaitAssertion(() => Assert.That(entities.GetComponent<TransformComponent>(user).MapUid, Is.EqualTo(arena.Owner)));
        await RunSeconds(server, 1.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<TransformComponent>(user).MapUid,
                Is.EqualTo(entities.GetComponent<TransformComponent>(gate).MapUid));
            Assert.That(entities.GetComponent<DamageableComponent>(user).TotalDamage.Float(), Is.LessThan(10),
                "The victory exit must not apply the five-HP pit penalty.");
            Assert.That(entities.HasComponent<BossArenaVisitorComponent>(user), Is.False);
        });
    }

    [Test]
    public async Task MissingOutsideGateStillReturnsDeadBodyFromVoidToArenaEntrance()
    {
        var (server,user,gate,key)=await SetupOutside();await OpenAndEnter(server,user,gate,key);
        var e=server.EntMan;var arena=e.AllEntities<BossArenaMapComponent>().Single();
        await server.WaitAssertion(()=>
        {
            e.DeleteEntity(gate);
            server.System<SharedTransformSystem>().SetCoordinates(user,new EntityCoordinates(arena.Owner,100,100));
        });
        await RunSeconds(server,2.1);
        await server.WaitAssertion(()=>AssertDeadAtEntrance(server,user,arena));
    }

    [Test]
    public async Task PausedFallWaitsThenLeavesIncapacitatedBodyDeadAtEntrance()
    {
        var (server,user,gate,key)=await SetupOutside();await OpenAndEnter(server,user,gate,key);
        var e=server.EntMan;var arena=e.AllEntities<BossArenaMapComponent>().Single();
        await server.WaitAssertion(()=>
        {
            server.System<SharedTransformSystem>().SetCoordinates(user,new EntityCoordinates(arena.Owner,100,100));
            Assert.That(server.System<BossArenaMapSystem>().TryReturnFromPit(user,arena.Owner,arena.Comp),Is.True);
            server.System<SharedMapSystem>().SetPaused(e.GetComponent<TransformComponent>(arena.Owner).MapID,true);
        });
        await RunSeconds(server,3);
        await server.WaitAssertion(()=>
        {
            Assert.That(e.HasComponent<BossArenaFallVisualComponent>(user),Is.True);
            server.System<SharedMapSystem>().SetPaused(e.GetComponent<TransformComponent>(arena.Owner).MapID,false);
            e.RemoveComponent<PassiveDamageComponent>(user);
            server.System<DamageableSystem>().SetDamage(user,e.GetComponent<DamageableComponent>(user),
                new DamageSpecifier{DamageDict={["Blunt"]=FixedPoint2.New(160)}});
        });
        await RunSeconds(server,2.1);
        await server.WaitAssertion(()=>AssertDeadAtEntrance(server,user,arena));
    }

    [Test]
    public async Task GateDeletedDuringFallRestoresBodyAndOrdinaryChasmStillDeletes()
    {
        var (server, user, gate, key) = await SetupOutside();
        await OpenAndEnter(server, user, gate, key);
        var entities = server.EntMan;
        var arena = entities.AllEntities<BossArenaMapComponent>().Single();
        EntityUid ordinaryVictim = default;
        await server.WaitAssertion(() =>
        {
            server.System<SharedTransformSystem>().SetCoordinates(user, new EntityCoordinates(arena.Owner, 100, 100));
            Assert.That(server.System<BossArenaMapSystem>().TryReturnFromPit(user, arena.Owner, arena.Comp), Is.True);
            entities.DeleteEntity(gate);
            ordinaryVictim = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            var normalFall = entities.AddComponent<ChasmFallingComponent>(ordinaryVictim);
            normalFall.NextDeletionTime = server.ResolveDependency<IGameTiming>().CurTime + normalFall.DeletionTime;
        });
        await RunSeconds(server, 2.1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(user), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(user).MapUid, Is.EqualTo(arena.Owner));
            Assert.That(entities.HasComponent<BossArenaFallVisualComponent>(user), Is.False);
            Assert.That(entities.HasComponent<BossArenaFallingComponent>(user), Is.False);
            Assert.That(entities.EntityExists(ordinaryVictim), Is.False, "Ordinary chasms retain their destructive behavior.");
        });
    }

    private static async Task OpenAndEnter(ServerIntegrationInstance server, EntityUid user, EntityUid gate, EntityUid key)
    {
        await server.WaitAssertion(() => Assert.That(server.System<BossArenaGateSystem>().TryUnlock(gate, user, key), Is.True));
        await RunSeconds(server, 3.2);
        await server.WaitAssertion(() => Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, user), Is.True));
        await RunSeconds(server, 5.2);
        await RunSeconds(server, 1.1);
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<BossArenaVisitorComponent>(user), Is.True));
        await server.WaitAssertion(() =>
        {
            var arena = server.EntMan.AllEntities<BossArenaMapComponent>().Single();
            server.EntMan.DeleteEntity(arena.Comp.Crystal!.Value);
            arena.Comp.Crystal = null;
            arena.Comp.Boss = server.EntMan.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
        });
    }

    [Test]
    public async Task CombatGraceAdmitsLatePlayerThenClosureCancelsPendingChannelAndRejectsKey()
    {
        var (server, user, gate, key) = await SetupOutside();
        await server.WaitAssertion(() => Assert.That(server.System<BossArenaGateSystem>().TryUnlock(gate, user, key), Is.True));
        await RunSeconds(server, 3.2);
        EntityUid tank = default;
        Entity<BossArenaMapComponent> arena = default;
        await server.WaitAssertion(() =>
        {
            arena = server.EntMan.AllEntities<BossArenaMapComponent>().Single();
            key = GiveKey(server, user);
            tank = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(tank);
            server.EntMan.RemoveComponent<ShieldOnStartupComponent>(tank);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(100);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Crystal!.Value, damage, origin: tank);
            Assert.That(server.EntMan.EntityExists(arena.Comp.Boss), Is.True);
            server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss).TankDamage = new DamageSpecifier();
        });
        await server.WaitAssertion(() =>
        {
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss);
            rotation.TankDamage = new DamageSpecifier();
            Assert.That(rotation.FightRunning, Is.True);
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, user), Is.True);
        });
        await RunSeconds(server, 5.2);
        await RunSeconds(server, 1.1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss).Party, Does.Contain(user));
            Assert.That(server.EntMan.GetComponent<BossComponent>(arena.Comp.Boss).Players, Does.Contain(user));
            var gates = server.System<BossArenaGateSystem>();
            Assert.That(gates.TryGetGateFront(user, gate, out var outside), Is.True);
            gates.TransportUser(user, outside);
            server.System<BossArenaMapSystem>().LeaveArena(user, arena.Owner);
        });
        await RunSeconds(server, 1.1);
        await server.WaitAssertion(() =>
        {
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss);
            rotation.EntryCloses = server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(2);
            var outsideGrid = server.EntMan.GetComponent<TransformComponent>(gate).GridUid!.Value;
            server.System<SharedTransformSystem>().SetCoordinates(user, new EntityCoordinates(outsideGrid, 0.5f, -0.5f));
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, user), Is.True);
        });
        await RunSeconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TransformComponent>(user).MapUid,
                Is.EqualTo(server.EntMan.GetComponent<TransformComponent>(gate).MapUid));
            Assert.That(server.EntMan.GetComponent<BossArenaGateComponent>(gate).Open, Is.False);
            Assert.That(server.System<BossArenaGateSystem>().TryUnlock(gate, user, key), Is.False);
        });
    }

    private static void AssertDeadAtEntrance(ServerIntegrationInstance server,EntityUid user,Entity<BossArenaMapComponent> arena)
    {
        var e=server.EntMan;var x=e.GetComponent<TransformComponent>(user);
        var offset=e.GetComponent<BossArenaEntranceComponent>(arena.Comp.Entrance).ArrivalOffset;
        var transform=server.System<SharedTransformSystem>();
        var local=transform.ToCoordinates(arena.Comp.Entrance,transform.ToMapCoordinates(x.Coordinates)).Position;
        Assert.That(x.MapUid,Is.EqualTo(arena.Owner));
        Assert.That(Vector2.Distance(local,offset),Is.LessThan(.001));
        Assert.That(server.System<MobStateSystem>().IsDead(user),Is.True);
        Assert.That(e.HasComponent<BossArenaFallingComponent>(user),Is.False);
        Assert.That(e.HasComponent<BossArenaFallVisualComponent>(user),Is.False);
        Assert.That(e.HasComponent<BossArenaPartyMemberComponent>(user),Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EveryGateOwnsItsArenaWhileTheSameGateReusesItsOwnInstance(bool copiedDestination)
    {
        var (server,user,gate,key)=await SetupOutside();
        await server.WaitAssertion(()=>
        {
            var e=server.EntMan;var maps=server.System<BossArenaMapSystem>();
            var a=e.GetComponent<BossArenaGateComponent>(gate);
            Assert.That(maps.TryEnsureArena(gate,a),Is.True);
            var first=a.Destination;
            var other=e.SpawnEntity("RevenantBossGate",new EntityCoordinates(gate,3,0));
            var b=e.GetComponent<BossArenaGateComponent>(other);
            if(copiedDestination)
            {
                b.Destination=first;
                var gates=server.System<BossArenaGateSystem>();
                Assert.That(gates.SetOpen(other,true),Is.True);
                server.System<SharedTransformSystem>().SetCoordinates(user,new EntityCoordinates(other,0,-1));
                Assert.That(gates.TryEnter(other,user),Is.True);
            }
            else Assert.That(maps.TryEnsureArena(other,b),Is.True);
            Assert.That(b.Destination,Is.Not.EqualTo(first));
            Assert.That(e.GetComponent<TransformComponent>(b.Destination!.Value).MapUid,
                Is.Not.EqualTo(e.GetComponent<TransformComponent>(first!.Value).MapUid));
            Assert.That(maps.TryEnsureArena(gate,a),Is.True);
            Assert.That(a.Destination,Is.EqualTo(first));
            Assert.That(server.System<BossArenaGateSystem>().LinkGate(other,first.Value),Is.True);
            Assert.That(b.Destination,Is.Not.EqualTo(first));
        });
    }

    private static EntityUid GiveKey(ServerIntegrationInstance server, EntityUid user)
    {
        var key = server.EntMan.SpawnEntity("RevenantBossGateKey", server.EntMan.GetComponent<TransformComponent>(user).Coordinates);
        Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, key), Is.True);
        return key;
    }

    private async Task<(ServerIntegrationInstance Server, EntityUid User, EntityUid Gate, EntityUid Key)> SetupOutside()
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
        EntityUid user = default, gate = default, key = default;
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var air = new GasMixture(2500) { Temperature = 293.15f };
            air.SetMoles(Gas.Oxygen, 21.82478f);
            air.SetMoles(Gas.Nitrogen, 82.10312f);
            server.System<AtmosphereSystem>().SetMapAtmosphere(map, false, air);
            var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            for (var x = -5; x <= 5; x++)
            for (var y = -5; y <= 5; y++)
                maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
            gate = server.EntMan.SpawnEntity("RevenantBossGate", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            user = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid.Owner, 0.5f, -0.5f));
            server.PlayerMan.SetAttachedEntity(sessions[0], user);
            key = server.EntMan.SpawnEntity("RevenantBossGateKey", new EntityCoordinates(grid.Owner, 0.5f, -0.5f));
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, key), Is.True);
        });
        return (server, user, gate, key);
    }

    private static Task RunSeconds(ServerIntegrationInstance server, double seconds)
        => server.WaitRunTicks((int) Math.Ceiling(server.ResolveDependency<IGameTiming>().TickRate * seconds) + 1);
}
