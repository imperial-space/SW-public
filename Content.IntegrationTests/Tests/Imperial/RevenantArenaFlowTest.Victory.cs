using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class RevenantArenaFlowTest
{
    [Test]
    public async Task VictoryLootTimerPausesThenReturnsEveryBodyClosesGateAndRequiresANewKey()
    {
        var (server, user, gate, key) = await SetupOutside();
        await OpenAndEnter(server, user, gate, key);
        var entities = server.EntMan;
        var arena = entities.AllEntities<BossArenaMapComponent>().Single();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid mate = default, oldBoss = default;
        EntityUid looseItem = default, pitItem = default, cofferItem = default, carriedItem = default, outsideItem = default;
        TimeSpan remaining = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(key), Is.False);
            arena.Comp.VictoryLootDuration = TimeSpan.FromMinutes(1);
            oldBoss = arena.Comp.Boss;
            looseItem = entities.SpawnEntity("RevenantBossGateKey", new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 1.5f));
            pitItem = entities.SpawnEntity("RevenantBossGateKey", new EntityCoordinates(arena.Owner, 10.5f, 10.5f));
            carriedItem = GiveKey(server, user);
            outsideItem = entities.SpawnEntity("RevenantBossGateKey", entities.GetComponent<TransformComponent>(gate).Coordinates);
            mate = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 2.5f));
            server.System<BossArenaMapSystem>().RecordEntry(mate, gate, arena.Comp.Entrance);
            entities.RemoveComponent<PassiveDamageComponent>(user);
            server.System<DamageableSystem>().SetDamage(user, entities.GetComponent<DamageableComponent>(user),
                new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(7) } });
            server.System<BossSystem>().DamageBoss(oldBoss, server.EntMan.GetComponent<BossComponent>(oldBoss).MaxHealth * 2);
            Assert.That(arena.Comp.VictoryDeadline, Is.EqualTo(timing.CurTime + TimeSpan.FromMinutes(1)));
        });
        await RunSeconds(server, 2.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(oldBoss), Is.False);
            Assert.That(entities.AllEntities<BossEncounterRewardComponent>(), Has.Length.EqualTo(1));
            var coffer = entities.AllEntities<BossEncounterRewardComponent>().Single().Owner;
            var storage = entities.GetComponent<EntityStorageComponent>(coffer);
            Assert.That(storage.Contents.ContainedEntities.Count(item =>
                entities.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == "RevenantBossScythe"), Is.EqualTo(1));
            Assert.That(storage.Contents.ContainedEntities.Count(item =>
                entities.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == "PlushieThrongler"), Is.EqualTo(2));
            var nativeStorage = server.System<SharedEntityStorageSystem>();
            Assert.That(storage.ShowContents || storage.Contents.ShowContents, Is.False);
            nativeStorage.OpenStorage(coffer);
            server.System<SharedTransformSystem>().SetCoordinates(user, new EntityCoordinates(coffer, 0, 0));
            var item = cofferItem = entities.SpawnEntity("RevenantBossGateKey", new EntityCoordinates(coffer, 0.4f, 0));
            nativeStorage.CloseStorage(coffer);
            Assert.That(storage.Contents.Contains(item), Is.True, "Closing captures loose items across the coffer's footprint.");
            Assert.That(storage.Contents.Contains(user), Is.True, "A party member can hide in this coffer.");
            var otherChest = entities.SpawnEntity("MedievalChest3", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            nativeStorage.OpenStorage(otherChest);
            Assert.That(nativeStorage.CanInsert(item, otherChest), Is.False);
            Assert.That(nativeStorage.CanInsert(user, otherChest), Is.False);
            Assert.That(server.System<SharedContainerSystem>().CanInsert(item,
                entities.GetComponent<EntityStorageComponent>(otherChest).Contents), Is.False);
            var timer = entities.GetComponent<BossArenaExitTimerComponent>(arena.Comp.VictoryExit!.Value);
            Assert.That(timer.EndTime, Is.EqualTo(arena.Comp.VictoryDeadline));
            Assert.That(timer.RemainingSeconds(timer.EndTime + TimeSpan.FromSeconds(1)), Is.Zero);
            remaining = arena.Comp.VictoryDeadline!.Value - timing.CurTime;
            server.System<SharedMapSystem>().SetPaused(entities.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await RunSeconds(server, 3);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<TransformComponent>(user).MapUid, Is.EqualTo(arena.Owner));
            foreach (var item in new[] { looseItem, pitItem, cofferItem })
                Assert.That(entities.EntityExists(item), Is.True, "Items remain available throughout the loot window.");
            server.System<SharedMapSystem>().SetPaused(entities.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That((arena.Comp.VictoryDeadline!.Value - timing.CurTime).TotalSeconds,
                Is.EqualTo(remaining.TotalSeconds).Within(0.05));
            Assert.That(entities.GetComponent<BossArenaExitTimerComponent>(arena.Comp.VictoryExit!.Value).EndTime,
                Is.EqualTo(arena.Comp.VictoryDeadline), "The visible timer shifts with the authoritative pause-safe deadline.");
            arena.Comp.VictoryDeadline = timing.CurTime;
            server.System<BossEncounterSystem>().Update(0);
            var outsideMap = entities.GetComponent<TransformComponent>(gate).MapUid;
            foreach (var player in new[] { user, mate })
            {
                Assert.That(entities.GetComponent<TransformComponent>(player).MapUid, Is.EqualTo(outsideMap));
                Assert.That(entities.HasComponent<BossArenaVisitorComponent>(player), Is.False);
                Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(player), Is.False);
                Assert.That(entities.HasComponent<InsideEntityStorageComponent>(player), Is.False);
            }
            Assert.That(entities.GetComponent<DamageableComponent>(user).TotalDamage.Float(), Is.EqualTo(7),
                "Victory timeout must not apply the pit's five-HP penalty.");
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.False);
            Assert.That(arena.Comp.VictoryDeadline, Is.Null);
            Assert.That(arena.Comp.VictoryExit, Is.Null);
            Assert.That(entities.AllEntities<BossEncounterRewardComponent>(), Is.Empty);
            foreach (var item in new[] { looseItem, pitItem, cofferItem })
                Assert.That(entities.EntityExists(item), Is.False, "Victory reset clears platform, abyss and coffer items.");
            Assert.That(entities.EntityExists(carriedItem), Is.True, "Players keep items carried outside.");
            Assert.That(entities.GetComponent<TransformComponent>(carriedItem).MapUid, Is.EqualTo(outsideMap));
            Assert.That(entities.EntityExists(outsideItem), Is.True, "Other maps are untouched.");
            foreach (var brazier in arena.Comp.Braziers)
                Assert.That(entities.EntityExists(brazier), Is.True, "Arena fixtures survive item cleanup.");
            Assert.That(arena.Comp.Boss, Is.Not.EqualTo(oldBoss));
            Assert.That(entities.EntityExists(arena.Comp.Boss), Is.False);
            Assert.That(entities.HasComponent<BossSummoningTriggerComponent>(arena.Comp.Crystal!.Value), Is.True);
            Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>(), Has.Length.EqualTo(1));
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, user), Is.False);
            Assert.That(server.System<BossArenaGateSystem>().TryUnlock(gate, user, key), Is.False);
        });
    }

    [Test]
    public async Task LastBodyLeavingClosesAllGatesResetsBeforeRewardSpawnAndOnlyAFreshKeyReopens()
    {
        var (server, user, gate, key) = await SetupOutside();
        await OpenAndEnter(server, user, gate, key);
        var entities = server.EntMan;
        var arena = entities.AllEntities<BossArenaMapComponent>().Single();
        var gates = server.System<BossArenaGateSystem>();
        EntityUid mate = default, secondGate = default, replacementKey = default, oldBoss = default;
        EntityUid leftover = default;
        await server.WaitAssertion(() =>
        {
            var outsideGrid = entities.GetComponent<TransformComponent>(gate).GridUid!.Value;
            secondGate = entities.SpawnEntity("RevenantBossGate", new EntityCoordinates(outsideGrid, 3.5f, 0.5f));
            Assert.That(gates.LinkGate(secondGate, arena.Comp.Entrance), Is.True);
            Assert.That(gates.SetOpen(secondGate, true), Is.True);
            var secondEntrance = entities.GetComponent<BossArenaGateComponent>(secondGate).Destination;
            Assert.That(secondEntrance, Is.Not.EqualTo(arena.Comp.Entrance));
            mate = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 2.5f));
            server.System<BossArenaMapSystem>().RecordEntry(mate, gate, arena.Comp.Entrance);
            oldBoss = arena.Comp.Boss;
            leftover = entities.SpawnEntity("RevenantBossGateKey", new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 1.5f));
            server.System<BossSystem>().DamageBoss(oldBoss, server.EntMan.GetComponent<BossComponent>(oldBoss).MaxHealth * 2);
            Assert.That(gates.TryGetGateFront(user, gate, out var outside), Is.True);
            gates.TransportUser(user, outside);
            server.System<BossArenaMapSystem>().LeaveArena(user, arena.Owner);
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(arena.Comp.Boss, Is.EqualTo(oldBoss), "The remaining disconnected/party body keeps loot available.");
            Assert.That(arena.Comp.VictoryDeadline, Is.Not.Null);
            Assert.That(entities.EntityExists(leftover), Is.True);
            gates.SetOpen(gate, false);
            replacementKey = GiveKey(server, user);
            Assert.That(gates.TryUnlock(gate, user, replacementKey), Is.False);
            Assert.That(entities.EntityExists(replacementKey), Is.True, "Loot-window rejection cannot consume another key.");
            Assert.That(gates.TryGetGateFront(mate, gate, out outside), Is.True);
            gates.TransportUser(mate, outside);
            server.System<BossArenaMapSystem>().LeaveArena(mate, arena.Owner);
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.False);
            Assert.That(entities.GetComponent<BossArenaGateComponent>(secondGate).Open, Is.True,
                "Leaving one completed encounter must not close another gate's arena.");
            Assert.That(entities.GetComponent<BossArenaGateComponent>(secondGate).Destination, Is.EqualTo(secondEntrance));
            Assert.That(arena.Comp.Boss, Is.Not.EqualTo(oldBoss));
            Assert.That(arena.Comp.VictoryExit, Is.Null);
            Assert.That(entities.EntityExists(leftover), Is.False, "Early departure cleans items just like timeout.");
            Assert.That(entities.AllEntities<BossEncounterRewardComponent>(), Is.Empty,
                "Early departure must remove a coffer spawned while deleting the defeated boss.");
        });
        await RunSeconds(server, 3);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.AllEntities<BossEncounterRewardComponent>(), Is.Empty);
            Assert.That(gates.TryEnter(gate, user), Is.False);
            Assert.That(gates.TryUnlock(gate, user, key), Is.False);
            Assert.That(server.System<SharedHandsSystem>().GetActiveItem(user), Is.EqualTo(replacementKey));
            server.System<SharedTransformSystem>().SetCoordinates(user,
                new EntityCoordinates(entities.GetComponent<TransformComponent>(gate).GridUid!.Value, 0.5f, -0.5f));
            Assert.That(gates.TryUnlock(gate, user, replacementKey), Is.True);
        });
        await RunSeconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.True);
            Assert.That(entities.EntityExists(replacementKey), Is.False);
            Assert.That(entities.AllEntities<BossArenaMapComponent>(), Has.Length.EqualTo(2));
            Assert.That(entities.AllEntities<BossComponent>(), Is.Empty);
            Assert.That(entities.HasComponent<BossSummoningTriggerComponent>(arena.Comp.Crystal!.Value), Is.True);
        });
    }

    [Test]
    public async Task ExpiredVictoryKeepsBodiesAndLootWhenGateIsMissingThenRecoversToItsReplacement()
    {
        var (server, user, gate, key) = await SetupOutside();
        await OpenAndEnter(server, user, gate, key);
        var entities = server.EntMan;
        var arena = entities.AllEntities<BossArenaMapComponent>().Single();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid outsideGrid = default;
        await server.WaitAssertion(() =>
        {
            outsideGrid = entities.GetComponent<TransformComponent>(gate).GridUid!.Value;
            server.System<BossSystem>().DamageBoss(arena.Comp.Boss, server.EntMan.GetComponent<BossComponent>(arena.Comp.Boss).MaxHealth * 2);
            entities.DeleteEntity(gate);
            arena.Comp.VictoryDeadline = timing.CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(entities.EntityExists(user), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(user).MapUid, Is.EqualTo(arena.Owner));
            Assert.That(arena.Comp.VictoryDeadline, Is.Not.Null);
        });
        await RunSeconds(server, 2.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.AllEntities<BossEncounterRewardComponent>(), Has.Length.EqualTo(1));
            var replacement = entities.SpawnEntity("RevenantBossGate", new EntityCoordinates(outsideGrid, 0.5f, 0.5f));
            Assert.That(server.System<BossArenaGateSystem>().LinkGate(replacement, arena.Comp.Entrance), Is.True);
            arena.Comp.NextVictoryReturn = timing.CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(entities.GetComponent<TransformComponent>(user).GridUid, Is.EqualTo(outsideGrid));
            Assert.That(arena.Comp.VictoryDeadline, Is.Null);
            Assert.That(entities.GetComponent<BossArenaGateComponent>(replacement).Open, Is.False);
            Assert.That(entities.AllEntities<BossEncounterRewardComponent>(), Is.Empty);
        });
    }
}
