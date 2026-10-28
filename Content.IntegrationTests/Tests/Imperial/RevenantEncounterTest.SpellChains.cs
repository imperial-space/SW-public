using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Imperial.Medieval.Magic.MedievalShootAfterSpawn;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(false)] [TestCase(true)]
    public async Task MeteorDelayedAndDespawnChainKeepsArenaOwnershipWithoutChangingOutsideDamage(bool outside)
    {
        var (server, arena, players, gate) = await Setup();
        var victim = players[1];
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transform = server.System<SharedTransformSystem>();
            var location = outside ? entities.GetComponent<TransformComponent>(gate).Coordinates
                : new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f);
            transform.SetCoordinates(victim, location);
            if (outside) transform.SetCoordinates(players[0], location);
            var spot = entities.SpawnEntity("MedievalMeteorSpot", location);
            server.System<SharedBossArenaRulesSystem>().TagSpell(spot, players[0]);
            entities.GetComponent<DelayedSpawnComponent>(spot).SpawnTime = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<DelayedSpawnSystem>().Update(0);
            var meteor = entities.AllEntities<SpawnOnDespawnComponent>().Single(entity =>
                entities.GetComponent<MetaDataComponent>(entity.Owner).EntityPrototype!.ID == "MedievalMeteor").Owner;
            Assert.That(entities.HasComponent<BossArenaSpellSourceComponent>(meteor), Is.EqualTo(!outside));
            var despawn = new TimedDespawnEvent();
            entities.EventBus.RaiseLocalEvent(meteor, ref despawn);
        });
        await CrystalSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(victim).TotalDamage.Float(), outside ? Is.GreaterThan(0) : Is.Zero);
            if (!outside) Assert.That(server.EntMan.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float(), Is.GreaterThan(0));
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task GroundSpikesAndLightColumnKeepFriendlyDamageAndIgnitionInsideArenaOnly(bool outside)
    {
        var (server, arena, players, gate) = await Setup();
        EntityUid spike = default, light = default;
        var victim = players[1];
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = server.System<SharedBossArenaRulesSystem>();
            var location = outside ? entities.GetComponent<TransformComponent>(gate).Coordinates
                : new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 3.5f);
            server.System<SharedTransformSystem>().SetCoordinates(victim, location);
            if (outside) server.System<SharedTransformSystem>().SetCoordinates(players[0], location.Offset(new System.Numerics.Vector2(2, 0)));
            var inactive = entities.SpawnEntity("MedievalProjectileInactiveSpikesBeginner", location);
            rules.TagSpell(inactive, players[0]);
            var despawn = new TimedDespawnEvent();
            entities.EventBus.RaiseLocalEvent(inactive, ref despawn);
            spike = entities.AllEntities<DamageContactsComponent>().Single(entity =>
                entities.GetComponent<MetaDataComponent>(entity.Owner).EntityPrototype?.ID == "MedievalProjectileActiveSpikeBeginner").Owner;
            Assert.That(entities.HasComponent<BossArenaSpellSourceComponent>(spike), Is.EqualTo(!outside));
            var spot = entities.SpawnEntity("MedievalProjectileSunstrikeSpotMiddle", location);
            rules.TagSpell(spot, players[0]);
            entities.GetComponent<DelayedSpawnComponent>(spot).SpawnTime = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<DelayedSpawnSystem>().Update(0);
            light = entities.AllEntities<DamageContactsComponent>().Single(entity =>
                entities.GetComponent<MetaDataComponent>(entity.Owner).EntityPrototype?.ID == "MedievalSunstrikeMiddle").Owner;
            Assert.That(entities.HasComponent<BossArenaSpellSourceComponent>(light), Is.EqualTo(!outside));
        });
        await CrystalSeconds(server, 0.5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<DamageableComponent>(victim).TotalDamage.Float(), outside ? Is.GreaterThan(0) : Is.Zero);
            Assert.That(entities.GetComponent<FlammableComponent>(victim).FireStacks, outside ? Is.GreaterThan(0) : Is.Zero);
            Assert.That(entities.HasComponent<DamagedByContactComponent>(victim), Is.EqualTo(outside));
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task FrostBeamPassesArenaAlliesAndHitsBossButStillHitsPeopleOutside(bool outside)
    {
        var (server, arena, players, gate) = await Setup();
        EntityUid gun = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transform = server.System<SharedTransformSystem>();
            var grid = outside ? entities.GetComponent<TransformComponent>(gate).Coordinates.EntityId : arena.Comp.CombatGrid;
            transform.SetCoordinates(players[0], new EntityCoordinates(grid, -4.5f, 0.5f));
            transform.SetCoordinates(players[1], new EntityCoordinates(grid, -1.5f, 0.5f));
            foreach (var other in players.Skip(2)) transform.SetCoordinates(other, new EntityCoordinates(arena.Comp.CombatGrid, -4.5f, -4.5f));
            gun = entities.SpawnEntity("MedievalEffectWaterSphereBeginner", new EntityCoordinates(grid, -3.5f, 0.5f));
            entities.RemoveComponent<MedievalShootAfterSpawnComponent>(gun);
            server.System<SharedBossArenaRulesSystem>().TagSpell(gun, players[0]);
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var grid = entities.GetComponent<TransformComponent>(gun).Coordinates.EntityId;
            Assert.That(server.System<GunSystem>().AttemptShoot(gun, gun, entities.GetComponent<GunComponent>(gun),
                new EntityCoordinates(grid, 0.5f, 0.5f)), Is.True);
            Assert.That(entities.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), outside ? Is.GreaterThan(0) : Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float(), outside ? Is.Zero : Is.GreaterThan(0));
        });
    }
}
