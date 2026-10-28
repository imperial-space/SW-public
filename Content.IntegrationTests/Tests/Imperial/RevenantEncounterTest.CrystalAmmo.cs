using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.MeleeParry.Components;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task CrystalCracksAtHalfHealthAndFinalAttackerOwnsTankFocus()
    {
        var (server, arena, players, gate) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var crystal = arena.Comp.Crystal!.Value;
            var summon = entities.GetComponent<BossSummoningTriggerComponent>(crystal);
            var damage = server.System<DamageableSystem>();
            var appearance = server.System<SharedAppearanceSystem>();
            Assert.That(summon.Health, Is.EqualTo(100));
            Assert.That(summon.SpawnCameraKick, Is.EqualTo(0.2f));
            Assert.That(summon.SpawnEffect!.Value.Id, Is.EqualTo("RevenantBossSummonBurst"));
            Assert.That(server.System<BossSummoningTriggerSystem>().TryActivate(crystal, players[0]), Is.False);
            damage.TryChangeDamage(crystal, Hit(49), origin: players[0]);
            Assert.That(entities.EntityExists(arena.Comp.Boss), Is.False);
            Assert.That(appearance.TryGetData(crystal, BossCrystalVisuals.Cracked, out bool cracked), Is.True);
            Assert.That(cracked, Is.False);
            damage.TryChangeDamage(crystal, Hit(1), origin: players[0]);
            Assert.That(appearance.TryGetData(crystal, BossCrystalVisuals.Cracked, out cracked), Is.True);
            Assert.That(cracked, Is.True);
            Assert.That(entities.EntityExists(arena.Comp.Boss), Is.False);
            damage.TryChangeDamage(crystal, Hit(50), origin: players[1]);
            var boss = arena.Comp.Boss;
            Assert.That(entities.AllEntities<BossComponent>(), Has.Length.EqualTo(1));
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.True);
            Assert.That(entities.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[1]));
            Assert.That(entities.GetComponent<BossComponent>(boss).Players, Is.EquivalentTo(players));
            Assert.That(arena.Comp.Crystal, Is.Null);
            Assert.That(entities.IsQueuedForDeletion(crystal), Is.True);
            damage.TryChangeDamage(crystal, Hit(100), origin: players[0]);
            Assert.That(entities.AllEntities<BossComponent>(), Has.Length.EqualTo(1));
            Assert.That(entities.AllEntities<AudioComponent>().Count(audio =>
            {
                var name = audio.Comp.FileName;
                return name.Contains("/Effects/glass_crack");
            }), Is.EqualTo(2));
            Assert.That(entities.AllEntities<AudioComponent>().Count(audio =>
            {
                var name = audio.Comp.FileName;
                return name.Contains("/Effects/glass_break");
            }), Is.EqualTo(1));
            Assert.That(entities.AllEntities<MetaDataComponent>().Count(entity =>
                entity.Comp.EntityPrototype?.ID == "RevenantBossSummonBurst"), Is.EqualTo(1));
            damage.TryChangeDamage(boss, Hit(1), origin: players[0]);
            Assert.That(entities.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[1]));
            var dropped = entities.SpawnEntity("MedievalArrow", new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 0.5f));
            Assert.That(server.System<BossEncounterSystem>().Reset(boss, arena), Is.True);
            var fresh = arena.Comp.Crystal!.Value;
            Assert.That(entities.GetComponent<DamageableComponent>(fresh).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.GetComponent<BossSummoningTriggerComponent>(fresh).Activating, Is.False);
            Assert.That(entities.EntityExists(dropped), Is.True);
            Assert.That(entities.GetComponent<BossArenaGateComponent>(gate).Open, Is.True);
        });
    }

    [TestCase("move")]
    [TestCase("damage")]
    [TestCase("repeat")]
    [TestCase("other-fight")]
    public async Task LegacyInteractionSummonInterruptedCastStaysUsableAndDoesNotPlayBreakSound(string reason)
    {
        var (server, arena, players, _) = await Setup(crystalOnly: true);
        var caster = players[0];
        await server.WaitAssertion(() =>
        {
            server.EntMan.GetComponent<BossSummoningTriggerComponent>(arena.Comp.Crystal!.Value).BreakToSummon = false;
            server.System<SharedTransformSystem>().SetCoordinates(caster, new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            Assert.That(server.System<BossSummoningTriggerSystem>().TryActivate(arena.Comp.Crystal!.Value, caster), Is.True);
        });
        await CrystalSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            switch (reason)
            {
                case "move":
                    server.System<SharedTransformSystem>().SetCoordinates(caster, new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 0.5f));
                    break;
                case "damage": server.System<DamageableSystem>().TryChangeDamage(caster, Hit(1)); break;
                case "repeat": server.System<BossSummoningTriggerSystem>().TryActivate(arena.Comp.Crystal!.Value, caster); break;
                case "other-fight": server.EntMan.EnsureComponent<BossLegacyParticipantComponent>(caster); break;
            }
        });
        await CrystalSeconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var crystal = arena.Comp.Crystal!.Value;
            Assert.That(entities.EntityExists(arena.Comp.Boss), Is.False);
            Assert.That(entities.GetComponent<BossSummoningTriggerComponent>(crystal).Activating, Is.False);
            Assert.That(entities.AllEntities<AudioComponent>().Any(audio =>
            {
                var name = audio.Comp.FileName;
                return name.Contains("/Effects/glass_break");
            }), Is.False);
            if (reason == "other-fight") entities.RemoveComponent<BossLegacyParticipantComponent>(caster);
            server.System<SharedTransformSystem>().SetCoordinates(caster, new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            Assert.That(server.System<BossSummoningTriggerSystem>().TryActivate(crystal, caster), Is.True);
        });
        await CrystalSeconds(server, 5.2);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(arena.Comp.Boss), Is.True));
    }

    private static Task CrystalSeconds(ServerIntegrationInstance server, double seconds)
        => server.WaitRunTicks((int)Math.Ceiling(server.ResolveDependency<IGameTiming>().TickRate * seconds) + 1);

    [TestCase(BossTelegraphPattern.Sinistra, "slash.ogg")]
    [TestCase(BossTelegraphPattern.Cross, "magic_hit.ogg")]
    [TestCase(BossTelegraphPattern.Divide, "magic_hit.ogg")]
    [TestCase(BossTelegraphPattern.Spread, "magic_hit.ogg")]
    [TestCase(BossTelegraphPattern.Stack, "magic_hit.ogg")]
    [TestCase(BossTelegraphPattern.BlackoutNorthWest, "magic_hit.ogg")]
    [TestCase(BossTelegraphPattern.PurpleFirst, "magic_hit.ogg")]
    public async Task CombatSoundPlaysOnceAtImpactAndNotDuringPreview(BossTelegraphPattern pattern, string file)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss,
                new BossTilePatternAttack { Telegraph = pattern, CastTime = 5, Damage = Hit(1) }), Is.True);
            int Sounds() => entities.AllEntities<AudioComponent>().Count(audio =>
            {
                var name = audio.Comp.FileName;
                return name.EndsWith("/RevenantBoss/" + file);
            });
            Assert.That(Sounds(), Is.Zero);
            entities.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(Sounds(), Is.EqualTo(1));
            var sound = entities.AllEntities<AudioComponent>().Single(audio =>
            {
                var name = audio.Comp.FileName;
                return name.EndsWith("/RevenantBoss/" + file);
            });
            Assert.That(sound.Comp.Params.Volume, Is.Zero);
            Assert.That(sound.Comp.Global, Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(sound.Owner).ParentUid, Is.EqualTo(arena.Owner));
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(Sounds(), Is.EqualTo(1));
        });
    }

    [TestCase("MedievalQuiverFullArrowIron")]
    [TestCase("MedievalQuiverFullArrowIronPhysics")]
    [TestCase("MedievalQuiverSteelFullArrowSteel")]
    [TestCase("MedievalQuiverNecro")]
    [TestCase("MedievalQuiverFullBoltIron")]
    [TestCase("MedievalQuiverFullBoltSteel")]
    [TestCase("MedievalQuiverSteelFullBoltIron")]
    [TestCase("MedievalQuiverSteelFullBoltSteel")]
    public async Task ArenaQuiversRefillWithoutExportingAmmoOrChangingOutsideStorage(string prototype)
    {
        var (server, arena, players, gate) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = server.System<SharedContainerSystem>();
            var storageSystem = server.System<SharedStorageSystem>();
            var quiver = entities.SpawnEntity(prototype, entities.GetComponent<TransformComponent>(players[0]).Coordinates);
            var storage = entities.GetComponent<StorageComponent>(quiver);
            if (storage.StoredItems.Count == 0)
            {
                var seed = entities.SpawnEntity("MedievalArrow", entities.GetComponent<TransformComponent>(quiver).Coordinates);
                Assert.That(storageSystem.Insert(quiver, seed, out _, out _), Is.True);
            }
            var count = storage.StoredItems.Count;
            var arrow = storage.StoredItems.Keys.First();
            var originalType = entities.GetComponent<MetaDataComponent>(arrow).EntityPrototype!.ID;
            Assert.That(containers.Remove(arrow, storage.Container), Is.True);
            Assert.That(storage.StoredItems.Count, Is.EqualTo(count));
            Assert.That(storage.StoredItems.Keys.Count(item => entities.GetComponent<MetaDataComponent>(item).EntityPrototype!.ID == originalType), Is.GreaterThan(0));
            Assert.That(entities.HasComponent<BossArenaAmmoComponent>(arrow), Is.True);
            Assert.That(storageSystem.Insert(quiver, arrow, out _, out _), Is.False);
            var outside = entities.GetComponent<TransformComponent>(gate).Coordinates;
            server.System<SharedTransformSystem>().SetCoordinates(arrow, outside);
            server.System<BossArenaAmmunitionSystem>().Update(0);
            Assert.That(entities.IsQueuedForDeletion(arrow), Is.True);
            server.System<SharedTransformSystem>().SetCoordinates(quiver, outside);
            var next = storage.StoredItems.Keys.First();
            Assert.That(containers.Remove(next, storage.Container), Is.True);
            Assert.That(storage.StoredItems.Count, Is.EqualTo(count - 1));
            Assert.That(entities.HasComponent<BossArenaAmmoComponent>(next), Is.False);
        });
    }

    [TestCase("MedievalWeaponCrossbow")]
    [TestCase("MedievalWeaponMiniCrossbow")]
    [TestCase("MedievalWeaponCrossbowSpeed")]
    public async Task ArenaCrossbowConsumesBoltAndRequiresNativeReloadFromUnlimitedQuiver(string prototype)
    {
        var (server, arena, players, gate) = await Setup(crystalOnly: true);
        EntityUid weapon = default, bolt = default;
        int count = 0;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var coordinates = entities.GetComponent<TransformComponent>(players[0]).Coordinates;
            weapon = entities.SpawnEntity(prototype, coordinates);
            var gun = entities.GetComponent<GunComponent>(weapon);
            var ammo = entities.GetComponent<BallisticAmmoProviderComponent>(weapon);
            var startingCount = ammo.Count;
            Assert.That(startingCount, Is.GreaterThan(0));
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(players[0], weapon), Is.True);
            Assert.That(server.System<GunSystem>().AttemptShoot(players[0], weapon, gun,
                new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f)), Is.True);
            Assert.That(ammo.Count, Is.EqualTo(startingCount - 1));
            if (ammo.Count > 0)
            {
                var remaining = new TakeAmmoEvent(ammo.Count, new(), coordinates, players[0]);
                entities.EventBus.RaiseLocalEvent(weapon, remaining);
                foreach (var (item, _) in remaining.Ammo) if (item is { } initial) entities.DeleteEntity(initial);
            }
            Assert.That(ammo.Count, Is.Zero);
            Assert.That(gun.NextFire, Is.GreaterThan(server.ResolveDependency<IGameTiming>().CurTime));
            var empty = new TakeAmmoEvent(1, new(), coordinates, players[0]);
            entities.EventBus.RaiseLocalEvent(weapon, empty);
            Assert.That(empty.Ammo, Is.Empty);
            Assert.That(server.System<GunSystem>().AttemptShoot(players[0], weapon, gun,
                new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f)), Is.False);
            var quiver = entities.SpawnEntity("MedievalQuiverFullBoltIron", coordinates);
            var storage = entities.GetComponent<StorageComponent>(quiver);
            count = storage.StoredItems.Count;
            bolt = storage.StoredItems.Keys.First();
            Assert.That(server.System<SharedContainerSystem>().Remove(bolt, storage.Container), Is.True);
            Assert.That(storage.StoredItems, Has.Count.EqualTo(count));
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(players[0], bolt), Is.True);
            var reload = new InteractUsingEvent(players[0], bolt, weapon, coordinates);
            entities.EventBus.RaiseLocalEvent(weapon, reload);
            Assert.That(reload.Handled, Is.True);
            Assert.That(ammo.Count, ammo.CartridgeFillDelay > TimeSpan.Zero ? Is.Zero : Is.EqualTo(1));
        });
        await CrystalSeconds(server, 6);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ammo = entities.GetComponent<BallisticAmmoProviderComponent>(weapon);
            Assert.That(ammo.Count, Is.EqualTo(1));
            var take = new TakeAmmoEvent(1, new(), entities.GetComponent<TransformComponent>(weapon).Coordinates, players[0]);
            entities.EventBus.RaiseLocalEvent(weapon, take);
            Assert.That(take.Ammo, Has.Count.EqualTo(1));
            Assert.That(take.Ammo[0].Entity, Is.EqualTo(bolt));
            Assert.That(ammo.Count, Is.Zero);
            var outside = entities.GetComponent<TransformComponent>(gate).Coordinates;
            server.System<SharedTransformSystem>().SetCoordinates(bolt, outside);
            server.System<BossArenaAmmunitionSystem>().Update(0);
            Assert.That(entities.IsQueuedForDeletion(bolt), Is.True);
        });
    }

    [TestCase("BowImprovised", "MedievalArrow")]
    [TestCase("BowImprovised", "MedievalArrowIronPhysics")]
    [TestCase("MedievalWeaponBow", "MedievalArrow")]
    [TestCase("MedievalWeaponBow", "MedievalArrowIronPhysics")]
    public async Task BorrowedArrowsLoadNormallyAndTheirShotsCannotBecomePermanentAmmo(string bowPrototype, string arrowPrototype)
    {
        var (server, arena, players, gate) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var coordinates = entities.GetComponent<TransformComponent>(players[0]).Coordinates;
            var quiver = entities.SpawnEntity("MedievalQuiver", coordinates);
            var storage = entities.GetComponent<StorageComponent>(quiver);
            var arrow = entities.SpawnEntity(arrowPrototype, coordinates);
            Assert.That(server.System<SharedStorageSystem>().Insert(quiver, arrow, out _, out _), Is.True);
            Assert.That(server.System<SharedContainerSystem>().Remove(arrow, storage.Container), Is.True);
            Assert.That(storage.StoredItems, Has.Count.EqualTo(1));
            var bow = entities.SpawnEntity(bowPrototype, coordinates);
            var defaults = new TakeAmmoEvent(1, new(), coordinates, null);
            entities.EventBus.RaiseLocalEvent(bow, defaults);
            foreach (var (item, _) in defaults.Ammo) if (item is { } initial) entities.DeleteEntity(initial);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(players[0], arrow), Is.True);
            var insert = new InteractUsingEvent(players[0], arrow, bow, coordinates);
            entities.EventBus.RaiseLocalEvent(bow, insert);
            Assert.That(insert.Handled, Is.True);
            var take = new TakeAmmoEvent(1, new(), coordinates, players[0]);
            entities.EventBus.RaiseLocalEvent(bow, take);
            Assert.That(take.Ammo, Has.Count.EqualTo(1));
            Assert.That(take.Ammo[0].Entity, Is.EqualTo(arrow));
            var projectile = entities.TryGetComponent<CartridgeAmmoComponent>(arrow, out var cartridge)
                ? entities.SpawnEntity(cartridge.Prototype, coordinates) : arrow;
            entities.EventBus.RaiseLocalEvent(bow, new AmmoShotEvent { FiredProjectiles = new() { projectile } });
            Assert.That(entities.GetComponent<BossArenaAmmoComponent>(projectile).Fired, Is.True);
            var empty = new TakeAmmoEvent(1, new(), coordinates, players[0]);
            entities.EventBus.RaiseLocalEvent(bow, empty);
            Assert.That(empty.Ammo, Is.Empty, "Bows must still reload between shots.");
            server.System<SharedTransformSystem>().SetCoordinates(projectile, entities.GetComponent<TransformComponent>(gate).Coordinates);
            server.System<BossArenaAmmunitionSystem>().Update(0);
            Assert.That(entities.IsQueuedForDeletion(projectile), Is.True);
            Assert.That(storage.StoredItems, Has.Count.EqualTo(1));
        });
    }
}
