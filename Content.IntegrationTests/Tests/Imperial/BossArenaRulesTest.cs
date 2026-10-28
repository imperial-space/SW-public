using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion;
using Content.Shared.Atmos.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Inventory;
using Content.Shared.Foldable;
using Content.Shared.Chemistry.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Server.Imperial.Medieval.Magic.MedievalSpellSleep;
using Content.Shared.Imperial.Minigames.Events;
using Content.Shared.Bed.Sleep;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tiles;
using Content.Shared.Maps;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossArenaRulesTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task WideSwingsReachRevenantFromEverySideButRespectRealWallsAndArenaScope()
    {
        var (server, arena, attacker, other) = await Setup();
        var entities = server.EntMan;
        var transforms = server.System<SharedTransformSystem>();
        var melee = server.System<SharedMeleeWeaponSystem>();
        var weapon = entities.GetComponent<MeleeWeaponComponent>(attacker);
        var health = entities.GetComponent<BossComponent>(arena.Comp.Boss);
        var bossPosition = new Vector2(0.5f, 0.5f);
        await server.WaitAssertion(() =>
        {
            transforms.SetCoordinates(other, new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            entities.GetComponent<BossEncounterComponent>(arena.Comp.Boss).AutoStart = true;
            server.System<SharedCombatModeSystem>().SetInCombatMode(attacker, true);
            weapon.Range = 1.6f;
        });
        foreach (var direction in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY })
        {
            await server.WaitAssertion(() => transforms.SetCoordinates(attacker,
                new EntityCoordinates(arena.Comp.CombatGrid, bossPosition + direction * 1.25f)));
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                var before = health.Health;
                weapon.NextAttack = server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(melee.AttemptHeavyAttack(attacker, attacker, weapon,
                    new EntityCoordinates(arena.Comp.CombatGrid, bossPosition)), Is.True);
                Assert.That(health.Health, Is.LessThan(before), $"Wide swing from {direction} must hit through the center barrier.");
                Assert.That(entities.GetComponent<BossHealthBarComponent>(arena.Comp.Boss).CurrentHp, Is.EqualTo(health.Health));
                Assert.That(entities.AllEntities<BossArenaCenterBlockerComponent>(), Has.Length.EqualTo(1));
            });
        }
        await server.WaitAssertion(() =>
        {
            Assert.That(health.Active, Is.True, "The first wide hit starts the encounter.");
            Assert.That(entities.GetComponent<BossFocusMarkerComponent>(arena.Comp.Boss).Tank, Is.EqualTo(attacker));
            transforms.SetCoordinates(attacker, new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 0.5f));
            weapon.Range = 3;
            entities.SpawnEntity("WallReinforced", new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var before = health.Health;
            weapon.NextAttack = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(melee.AttemptHeavyAttack(attacker, attacker, weapon,
                new EntityCoordinates(arena.Comp.CombatGrid, bossPosition)), Is.True);
            Assert.That(health.Health, Is.EqualTo(before), "Ordinary walls still block wide swings inside the arena.");
            entities.RemoveComponent<BossArenaRulesComponent>(arena.Owner);
            var blocker = entities.AllEntities<BossArenaCenterBlockerComponent>().Single().Owner;
            transforms.SetCoordinates(blocker, new EntityCoordinates(arena.Comp.CombatGrid, -0.2f, 0.5f));
            transforms.SetCoordinates(attacker, new EntityCoordinates(arena.Comp.CombatGrid, -0.75f, 0.5f));
            weapon.Range = 1.6f;
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var before = health.Health;
            weapon.NextAttack = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(melee.AttemptHeavyAttack(attacker, attacker, weapon,
                new EntityCoordinates(arena.Comp.CombatGrid, bossPosition)), Is.True);
            Assert.That(health.Health, Is.EqualTo(before), "An identical blocker outside the arena keeps native swing behavior.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PreciseAndWideSwingsReachTheExpandedBossFootprint(bool wide)
    {
        var (server, arena, attacker, other) = await Setup();
        var entities = server.EntMan;
        var transforms = server.System<SharedTransformSystem>();
        var melee = server.System<SharedMeleeWeaponSystem>();
        var weapon = entities.GetComponent<MeleeWeaponComponent>(attacker);
        var health = entities.GetComponent<BossComponent>(arena.Comp.Boss);
        var center = new Vector2(0.5f, 0.5f);
        await server.WaitAssertion(() =>
        {
            transforms.SetCoordinates(other, new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            server.System<SharedCombatModeSystem>().SetInCombatMode(attacker, true);
            weapon.Range = 1.6f;
        });
        foreach (var direction in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY })
        {
            var sideways = new Vector2(-direction.Y, direction.X);
            await server.WaitAssertion(() => transforms.SetCoordinates(attacker,
                new EntityCoordinates(arena.Comp.CombatGrid, center + direction * 1.5f + sideways * 0.95f)));
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                var before = health.Health;
                weapon.NextAttack = server.ResolveDependency<IGameTiming>().CurTime;
                Assert.That(wide ? melee.AttemptHeavyAttack(attacker, attacker, weapon,
                    new EntityCoordinates(arena.Comp.CombatGrid, center + sideways * 0.95f))
                    : melee.AttemptLightAttack(attacker, attacker, weapon, arena.Comp.Boss), Is.True);
                Assert.That(health.Health, Is.LessThan(before), $"A {(wide ? "wide" : "precise")} hit must reach the boss from {direction}.");
            });
        }
    }

    [Test]
    public async Task CenterTileBlocksBodiesButNotSpellPhysicsAndRemainsWhenBossMoves()
    {
        var (server, arena, mage, tank) = await Setup();
        EntityUid body = default, spell = default, center = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            server.System<SharedTransformSystem>().SetCoordinates(mage, new EntityCoordinates(arena.Comp.CombatGrid, -4.5f, 3.5f));
            server.System<SharedTransformSystem>().SetCoordinates(tank, new EntityCoordinates(arena.Comp.CombatGrid, -4.5f, -3.5f));
            center = entities.AllEntities<BossArenaCenterBlockerComponent>()
                .Single(ent => entities.GetComponent<TransformComponent>(ent.Owner).MapUid == arena.Owner).Owner;
            var fixture = entities.GetComponent<FixturesComponent>(center).Fixtures.Values.Single();
            Assert.That(fixture.CollisionLayer & (int)Content.Shared.Physics.CollisionGroup.MobMask, Is.Not.Zero);
            Assert.That(fixture.CollisionLayer & (int)(Content.Shared.Physics.CollisionGroup.Opaque |
                Content.Shared.Physics.CollisionGroup.BulletImpassable | Content.Shared.Physics.CollisionGroup.InteractImpassable), Is.Zero,
                "Center collision must not obstruct lighting, ranged targeting or melee reach.");
            body = entities.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, -1.5f, 0.5f));
            spell = entities.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, -1.5f, 0.5f));
            entities.AddComponent<Content.Shared.Mobs.Components.MobStateComponent>(body);

            foreach (var probe in new[] { body, spell })
            {
                entities.AddComponent<PhysicsComponent>(probe);
                entities.EnsureComponent<FixturesComponent>(probe);
                Assert.That(server.System<FixtureSystem>().TryCreateFixture(probe,
                    new PhysShapeCircle(0.2f), "probe",
                    collisionLayer: (int)Content.Shared.Physics.CollisionGroup.MobLayer,
                    collisionMask: (int)Content.Shared.Physics.CollisionGroup.MobMask, friction: 0), Is.True);
                server.System<SharedPhysicsSystem>().SetBodyType(probe, BodyType.Dynamic);
                server.System<SharedPhysicsSystem>().SetCanCollide(probe, true);
                server.System<SharedPhysicsSystem>().SetLinearVelocity(probe, new Vector2(4, 0));
            }
            server.System<BossArenaProtectionSystem>().MoveBoss(arena.Comp.Boss,
                new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
        });

        for (var step = 0; step < 15; step++)
        {
            await server.WaitAssertion(() =>
            {
                server.System<SharedPhysicsSystem>().SetLinearVelocity(body, new Vector2(4, 0));
                server.System<SharedPhysicsSystem>().SetLinearVelocity(spell, new Vector2(4, 0));
            });
            await server.WaitRunTicks(3);
        }
        await server.WaitAssertion(() =>
        {
            var transforms = server.System<SharedTransformSystem>();
            Assert.That(transforms.GetWorldPosition(body).X, Is.InRange(-0.3f, 0f),
                "A moving body stops at the center tile boundary even with the boss displaced.");
            Assert.That(transforms.GetWorldPosition(spell).X, Is.GreaterThan(1f),
                "The identical non-body probe crosses the center, proving collision filtering permits spells.");
            Assert.That(transforms.GetWorldPosition(center), Is.EqualTo(new Vector2(0.5f, 0.5f)));
            server.EntMan.DeleteEntity(arena.Comp.Boss);
            Assert.That(server.EntMan.EntityExists(center), Is.True, "The center blocker belongs to the arena, not the boss.");
        });
    }

    [Test]
    public async Task PartyDamageDoesNotWearArmorButHealingCostsAndBossDamageWork()
    {
        var (server, arena, mage, tank) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var damage = server.System<DamageableSystem>();
            var rules = server.System<SharedBossArenaRulesSystem>();
            var armor = entities.SpawnEntity("MedievalClothingOuterArmorIron", new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 0.5f));
            Assert.That(server.System<InventorySystem>().TryEquip(tank, armor, "outerClothing", force: true), Is.True);
            var integrity = entities.GetComponent<MedievalArmorIntegrityComponent>(armor);
            var durability = integrity.CurrentArmorHP;
            damage.TryChangeDamage(tank, Spec("Slash", 60), origin: mage);
            Assert.That(entities.GetComponent<DamageableComponent>(tank).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(integrity.CurrentArmorHP, Is.EqualTo(durability));
            damage.TryChangeDamage(tank, Spec("Slash", 60), origin: arena.Comp.Boss);
            Assert.That(entities.GetComponent<DamageableComponent>(tank).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(integrity.CurrentArmorHP, Is.EqualTo(durability));
            var protection = entities.GetComponent<DamageableComponent>(tank).TotalDamage.Float();
            Assert.That(protection, Is.LessThan(60), "Boss damage still respects armor protection.");
            var outside = server.System<SharedMapSystem>().CreateMap(out _, runMapInit: true);
            server.System<SharedTransformSystem>().SetCoordinates(tank, new EntityCoordinates(outside, 0, 0));
            damage.TryChangeDamage(tank, Spec("Slash", 10), origin: arena.Comp.Boss);
            Assert.That(integrity.CurrentArmorHP, Is.LessThan(durability), "The durability exemption is limited to this arena.");
            server.System<SharedTransformSystem>().SetCoordinates(tank, new EntityCoordinates(arena.Comp.CombatGrid, -0.5f, 0.5f));
            damage.SetDamage(tank, entities.GetComponent<DamageableComponent>(tank), Spec("Slash", 10));
            rules.ApplySpellEffect(new HealthChange { Damage = Spec("Slash", -5) }, new EntityEffectBaseArgs(tank, entities, mage), mage);
            Assert.That(entities.GetComponent<DamageableComponent>(tank).TotalDamage.Float(), Is.EqualTo(5));
            var mixed = Spec("Slash", -2);
            mixed.DamageDict["Blunt"] = FixedPoint2.New(50);
            damage.TryChangeDamage(tank, mixed, origin: mage);
            Assert.That(entities.GetComponent<DamageableComponent>(tank).TotalDamage.Float(), Is.EqualTo(3));
            damage.TryChangeDamage(mage, Spec("Blunt", 5));
            Assert.That(entities.GetComponent<DamageableComponent>(mage).TotalDamage.Float(), Is.EqualTo(5));
            damage.TryChangeDamage(arena.Comp.Boss, Spec("Heat", 25), origin: mage);
            Assert.That(entities.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float(), Is.EqualTo(25));
            var flame = entities.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 0.5f));
            rules.TagSpell(flame, mage);
            var secondary = entities.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 0.5f));
            rules.TagSpell(secondary, flame);
            server.System<SharedTransformSystem>().SetCoordinates(mage, new EntityCoordinates(arena.Owner, 100, 100));
            entities.DeleteEntity(flame);
            damage.TryChangeDamage(tank, Spec("Heat", 50), origin: secondary);
            Assert.That(entities.GetComponent<DamageableComponent>(tank).TotalDamage.Float(), Is.EqualTo(3));
        });
    }

    [TestCase("Beginner")]
    [TestCase("Middle")]
    [TestCase("Senior")]
    public async Task RealFireballExplosionProtectsPartyAndKeepsDamagingBoss(string tier)
    {
        var (server, arena, mage, tank) = await Setup();
        float initialBoss = 0;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            initialBoss = entities.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float();
            var fireball = entities.SpawnEntity("MedievalProjectileFireball" + tier,
                new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            server.System<SharedBossArenaRulesSystem>().TagSpell(fireball, mage);

            server.System<ExplosionSystem>().TriggerExplosive(fireball);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float(), Is.GreaterThan(initialBoss));
            foreach (var body in new[] { mage, tank })
            {
                Assert.That(entities.GetComponent<DamageableComponent>(body).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                var fire = entities.GetComponent<FlammableComponent>(body);
                Assert.That(fire.FireStacks, Is.Zero);
                Assert.That(fire.OnFire, Is.False);
                Assert.That(entities.GetComponent<PhysicsComponent>(body).LinearVelocity.Length(), Is.LessThan(0.01f));
            }
            var grid = entities.GetComponent<MapGridComponent>(arena.Comp.CombatGrid);
            Assert.That(server.System<SharedMapSystem>().GetAllTiles(arena.Comp.CombatGrid, grid).Count(), Is.EqualTo(121));
        });
    }

    [Test]
    public async Task OffensiveProjectilePassesAlliesAndControlCannotDisableBoss()
    {
        var (server, arena, mage, tank) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = server.System<SharedBossArenaRulesSystem>();
            var projectile = entities.SpawnEntity("MedievalProjectileFireballBeginner", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 0.5f));
            server.System<SharedGunSystem>().ShootProjectile(projectile, Vector2.UnitX, Vector2.Zero, mage, mage);
            var fixtures = entities.GetComponent<FixturesComponent>(projectile);
            var tankFixtures = entities.GetComponent<FixturesComponent>(tank);
            var collision = new PreventCollideEvent(projectile, tank, entities.GetComponent<PhysicsComponent>(projectile),
                entities.GetComponent<PhysicsComponent>(tank), fixtures.Fixtures["projectile"], tankFixtures.Fixtures.Values.First());
            entities.EventBus.RaiseLocalEvent(projectile, ref collision);
            Assert.That(collision.Cancelled, Is.True);
            var bossCollision = new PreventCollideEvent(projectile, arena.Comp.Boss, entities.GetComponent<PhysicsComponent>(projectile),
                entities.GetComponent<PhysicsComponent>(arena.Comp.Boss), fixtures.Fixtures["projectile"],
                entities.GetComponent<FixturesComponent>(arena.Comp.Boss).Fixtures.Values.First());
            entities.EventBus.RaiseLocalEvent(projectile, ref bossCollision);
            Assert.That(bossCollision.Cancelled, Is.False);
            entities.DeleteEntity(projectile);
            using (rules.WithSource(mage))
                Assert.That(server.System<SharedStunSystem>().TryAddParalyzeDuration(tank, TimeSpan.FromSeconds(5)), Is.False);
            Assert.That(server.System<SharedStunSystem>().TryAddParalyzeDuration(arena.Comp.Boss, TimeSpan.FromSeconds(5)), Is.False);
            var transforms = server.System<SharedTransformSystem>();
            var before = entities.GetComponent<TransformComponent>(arena.Comp.Boss).Coordinates;
            transforms.SetCoordinates(arena.Comp.Boss, new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 3.5f));
            Assert.That(entities.GetComponent<TransformComponent>(arena.Comp.Boss).Coordinates, Is.EqualTo(before));
            server.System<BossArenaProtectionSystem>().MoveBoss(arena.Comp.Boss, new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 3.5f));
            Assert.That(entities.GetComponent<TransformComponent>(arena.Comp.Boss).Coordinates.Position, Is.EqualTo(new Vector2(3.5f, 3.5f)));
            server.System<BossArenaProtectionSystem>().MoveBoss(arena.Comp.Boss, before);
            Assert.That(entities.GetComponent<TransformComponent>(arena.Comp.Boss).Anchored, Is.True);
        });
    }

    [Test]
    public async Task ContainersTerrainAndSecondaryRockSpawnsCannotBypassArenaRules()
    {
        var (server, arena, mage, tank) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = server.System<SharedBossArenaRulesSystem>();
            var containers = server.System<SharedContainerSystem>();
            var locker = entities.SpawnEntity("LockerSteel", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            var storage = entities.GetComponent<EntityStorageComponent>(locker);
            Assert.That(containers.CanInsert(arena.Comp.Boss, storage.Contents), Is.False);
            Assert.That(containers.CanInsert(tank, storage.Contents), Is.False);
            var entityStorage = server.System<SharedEntityStorageSystem>();
            entityStorage.OpenStorage(locker);
            Assert.That(entityStorage.CanInsert(arena.Comp.Boss, locker), Is.False, "Open lockers must not skip protection.");
            entityStorage.CloseStorage(locker);
            Assert.That(containers.IsEntityInContainer(arena.Comp.Boss), Is.False);
            Assert.That(containers.IsEntityInContainer(tank), Is.False);
            var coffer = entities.SpawnEntity("RevenantBossCoffer", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            var key = entities.SpawnEntity("RevenantBossGateKey", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            Assert.That(containers.CanInsert(key, entities.GetComponent<EntityStorageComponent>(coffer).Contents), Is.True);
            foreach (var prototype in new[] { "MedievalIceBarrierBeginner", "MedievalStoneRidgeBeginner", "MedievalShelterSenior", "MedievalShelterRock" })
            {
                Assert.That(rules.CanSpawn(prototype, new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 2.5f)), Is.False);
                Assert.That(rules.CanSpawn(prototype, new EntityCoordinates(arena.Owner, 30, 30)), Is.False);
            }
            Assert.That(rules.CanSpawn("MedievalProjectileFireballBeginner", new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 2.5f)), Is.True);
            entities.SpawnEntity("MedievalStoneRidgeBeginner", new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 2.5f));
            Assert.That(entities.AllEntities<MetaDataComponent>().Count(ent => ent.Comp.EntityPrototype?.ID == "MedievalShelterRock"), Is.Zero);
            var grid = entities.GetComponent<MapGridComponent>(arena.Comp.CombatGrid);
            Assert.That(server.System<FloorTileSystem>().CanPlaceTile(arena.Comp.CombatGrid, grid, new Vector2i(6, 0), out _), Is.False);
            var tile = server.System<SharedMapSystem>().GetTileRef(arena.Comp.CombatGrid, grid, new Vector2i(0, 0));
            Assert.That(server.System<TileSystem>().PryTile(tile), Is.False);
            Assert.That(server.System<TileSystem>().DeconstructTile(tile), Is.False);
        });
    }

    [Test]
    public async Task ArenaSceneryResistsDamageDestructionAndFloorExplosions()
    {
        var (server, arena, mage, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var outside = maps.CreateMap(out var outsideId, runMapInit: true);
            var outsideGrid = server.ResolveDependency<IMapManager>().CreateGridEntity(outsideId);
            maps.SetTile(outsideGrid.Owner, outsideGrid.Comp, new Vector2i(0, 0), new Tile(1));
            var gate = entities.SpawnEntity("RevenantBossGate", new EntityCoordinates(outsideGrid.Owner, 0.5f, 0.5f));
            var wall = entities.AllEntities<MetaDataComponent>().First(ent => ent.Comp.EntityPrototype?.ID == "RevenantBossArenaWall").Owner;
            foreach (var scenery in arena.Comp.Braziers.Append(gate).Append(wall))
            {
                Assert.That(entities.HasComponent<GodmodeComponent>(scenery), Is.True);

                var damageable = entities.EnsureComponent<DamageableComponent>(scenery);
                Assert.That(server.System<DamageableSystem>().TryChangeDamage(scenery,
                    Spec("Slash", 10000), ignoreResistances: true, origin: mage), Is.Null);
                Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(server.System<Content.Server.Destructible.DestructibleSystem>().DestroyEntity(scenery, mage), Is.False);
            }
            var grid = entities.GetComponent<MapGridComponent>(arena.Comp.CombatGrid);
            var floor = maps.GetTileRef(arena.Comp.CombatGrid, grid, new Vector2i(0, 0));
            var definitions = server.ResolveDependency<ITileDefinitionManager>();
            Assert.That(definitions[floor.Tile.TypeId].ID, Does.StartWith("RevenantBossArenaFloor"));
            Assert.That(((ContentTileDefinition)definitions[floor.Tile.TypeId]).Indestructible, Is.True);
            var damaged = new List<(Vector2i GridIndices, Tile Tile)>();
            server.System<ExplosionSystem>().DamageFloorTile(floor, 10000, 10, true, damaged,
                server.ResolveDependency<IPrototypeManager>().Index<ExplosionPrototype>("Default"));
            Assert.That(damaged, Is.Empty, "Even direct explosion processing cannot remove arena floor.");
            Assert.That(maps.GetTileRef(arena.Comp.CombatGrid, grid, new Vector2i(0, 0)).Tile,
                Is.EqualTo(floor.Tile), "The authored floor tile must survive unchanged.");
            var pit = entities.AllEntities<BossArenaChasmComponent>().First();
            var pitGrid = entities.GetComponent<TransformComponent>(pit.Owner).GridUid!.Value;
            var pitTile = maps.GetAllTiles(pitGrid, entities.GetComponent<MapGridComponent>(pitGrid))
                .First(tile => definitions[tile.Tile.TypeId].ID == "RevenantBossArenaPitFloor");
            Assert.That(((ContentTileDefinition)definitions[pitTile.Tile.TypeId]).Indestructible, Is.True);

            Assert.That(entities.HasComponent<GodmodeComponent>(arena.Comp.Boss), Is.False);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Spec("Slash", 10), origin: mage);
            Assert.That(entities.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float(), Is.EqualTo(10));
        });
    }

    [Test]
    public async Task OutsideArenaKeepsNormalDamageAndBuilding()
    {
        var (server, arena, mage, tank) = await Setup();
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var outside = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            maps.SetTile(grid.Owner, grid.Comp, new Vector2i(0, 0), new Tile(1));
            foreach (var body in new[] { mage, tank })
                server.System<SharedTransformSystem>().SetCoordinates(body, new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            server.System<DamageableSystem>().TryChangeDamage(tank, Spec("Slash", 20), origin: mage);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(tank).TotalDamage.Float(), Is.GreaterThan(0));
            Assert.That(server.System<SharedBossArenaRulesSystem>().CanSpawn("MedievalIceBarrierBeginner", new EntityCoordinates(grid.Owner, 2.5f, 0.5f)), Is.True);
            Assert.That(server.System<FloorTileSystem>().CanPlaceTile(grid.Owner, grid.Comp, new Vector2i(1, 0), out _), Is.True);
            var rules = server.System<SharedBossArenaRulesSystem>();
            using (rules.WithSource(mage))
                Assert.That(rules.CurrentSource, Is.Null);
            var spell = server.EntMan.SpawnEntity("MedievalProjectileFireballBeginner", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            rules.TagSpell(spell, mage);
            Assert.That(server.EntMan.HasComponent<BossArenaSpellSourceComponent>(spell), Is.False);
            server.EntMan.EnsureComponent<BossArenaProtectedComponent>(tank);
            Assert.That(rules.BlocksControl(tank, mage), Is.False, "An encounter marker must not activate protections outside its arena.");
            var storage = server.EntMan.SpawnEntity("LockerSteel", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            Assert.That(rules.CanUseEntityStorage(tank, storage), Is.True);
            Assert.That(server.System<StatusEffectsSystem>().CanAddStatusEffect(tank, SharedStunSystem.StunId), Is.True);
            var speed = new MovespeedModifier { WalkSpeedModifier = 0.2f, SprintSpeedModifier = 0.2f };
            rules.ApplySpellEffect(speed, new EntityEffectBaseArgs(tank, server.EntMan, mage), mage);
            Assert.That(server.EntMan.GetComponent<MovespeedModifierMetabolismComponent>(tank).WalkSpeedModifier, Is.EqualTo(0.2f));
        });
    }

    [Test]
    public async Task FireballsOutsideArenaRemainUntaggedAndDamagePlayers()
    {
        var (server, _, mage, tank) = await Setup();
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            maps.CreateMap(out var mapId, runMapInit: true);
            var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            for (var x = -3; x <= 3; x++) for (var y = -3; y <= 3; y++)
                maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
            foreach (var body in new[] { mage, tank })
                server.System<SharedTransformSystem>().SetCoordinates(body, new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            var fireball = server.EntMan.SpawnEntity("MedievalProjectileFireballBeginner", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            server.System<SharedBossArenaRulesSystem>().TagSpell(fireball, mage);
            Assert.That(server.EntMan.HasComponent<BossArenaSpellSourceComponent>(fireball), Is.False);
            server.System<ExplosionSystem>().TriggerExplosive(fireball);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(tank).TotalDamage.Float(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task BeneficialEffectsStillWorkAndDirectControlCannotBypassRules()
    {
        var (server, arena, mage, tank) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = server.System<SharedBossArenaRulesSystem>();
            var args = new EntityEffectBaseArgs(tank, entities, mage);
            rules.ApplySpellEffect(new MovespeedModifier { WalkSpeedModifier = 1.2f, SprintSpeedModifier = 1.2f }, args, mage);
            Assert.That(entities.GetComponent<MovespeedModifierMetabolismComponent>(tank).WalkSpeedModifier, Is.EqualTo(1.2f));
            rules.ApplySpellEffect(new MovespeedModifier { WalkSpeedModifier = 0.2f, SprintSpeedModifier = 0.2f }, args, mage);
            Assert.That(entities.GetComponent<MovespeedModifierMetabolismComponent>(tank).WalkSpeedModifier, Is.EqualTo(1.2f));
            using (rules.WithSource(mage))
            {
                Assert.That(server.System<StatusEffectsSystem>().CanAddStatusEffect(tank, SharedStunSystem.StunId), Is.False);
                server.System<SharedFlashSystem>().Flash(tank, mage, null, TimeSpan.FromSeconds(5), 0.5f);
            }
            Assert.That(entities.HasComponent<FlashedComponent>(tank), Is.False);
            foreach (var body in new[] { tank, arena.Comp.Boss })
            {
                entities.EnsureComponent<MedievalSleepTargetComponent>(body).Caster = mage;
                entities.EventBus.RaiseLocalEvent(body, new LoseInMinigameEvent());
                Assert.That(entities.HasComponent<SleepingComponent>(body), Is.False);
            }
            Assert.That(server.System<StatusEffectsSystem>().CanAddStatusEffect(arena.Comp.Boss, SharedStunSystem.StunId), Is.False);

            var projectile = entities.SpawnEntity("MedievalProjectileFireballBeginner", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 0.5f));
            entities.GetComponent<ProjectileComponent>(projectile).Damage = Spec("Slash", -10);
            rules.TagSpell(projectile, mage);
            var collision = new PreventCollideEvent(projectile, tank, entities.GetComponent<PhysicsComponent>(projectile),
                entities.GetComponent<PhysicsComponent>(tank), entities.GetComponent<FixturesComponent>(projectile).Fixtures["projectile"],
                entities.GetComponent<FixturesComponent>(tank).Fixtures.Values.First());
            entities.EventBus.RaiseLocalEvent(projectile, ref collision);
            Assert.That(collision.Cancelled, Is.False);

            entities.GetComponent<ProjectileComponent>(projectile).Damage = new DamageSpecifier();
            collision.Cancelled = false;
            entities.EventBus.RaiseLocalEvent(projectile, ref collision);
            Assert.That(collision.Cancelled, Is.True);
            entities.DeleteEntity(projectile);
            using (rules.WithSource(mage))
            {
                using (rules.WithSource(arena.Comp.Boss))
                    Assert.That(rules.CurrentSource, Is.Null);
                Assert.That(rules.CurrentSource?.Player, Is.EqualTo(mage));
            }
            Assert.That(rules.CurrentSource, Is.Null);
        });
    }

    [Test]
    public async Task EquipmentImportsAndObstacleCastsAreRejectedButHealingIsAllowed()
    {
        var (server, arena, mage, tank) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = server.System<SharedBossArenaRulesSystem>();
            var containers = server.System<SharedContainerSystem>();
            var backpack = entities.SpawnEntity("ClothingBackpack", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            var carried = containers.EnsureContainer<Container>(mage, "arena-test-equipment");
            Assert.That(containers.Insert(backpack, carried), Is.True);
            Assert.That(rules.CanBringEquipment(mage), Is.True);
            var hidden = containers.EnsureContainer<Container>(backpack, "arena-test-nested");
            var locker = entities.SpawnEntity("LockerSteel", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            Assert.That(containers.Insert(locker, hidden), Is.True);
            Assert.That(rules.CanBringEquipment(mage), Is.False);
            var folded = entities.SpawnEntity("ChairFoldingSpawnFolded", new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 4.5f));
            var foldable = entities.GetComponent<FoldableComponent>(folded);
            Assert.That(server.System<FoldableSystem>().TrySetFolded(folded, foldable, false), Is.False);
            Assert.That(foldable.IsFolded, Is.True);
            var action = entities.SpawnEntity("MedievalActionIceBarrierBeginner", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            Assert.That(rules.CanCast(action, mage, entities.GetComponent<TransformComponent>(mage).Coordinates), Is.False);
            var heal = entities.SpawnEntity("MedievalActionHolyTouchBeginner", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            Assert.That(rules.CanCast(heal, mage, entities.GetComponent<TransformComponent>(tank).Coordinates), Is.True);
            var mana = entities.EnsureComponent<ManaComponent>(mage);
            mana.MaxMana = 1000;
            mana.Mana = 500;
            var rejected = new MedievalSpawnSpellEvent
            {
                Action = action, Performer = mage, Target = entities.GetComponent<TransformComponent>(mage).Coordinates,
                SpawnedEntityPrototype = "MedievalIceBarrierBeginner", SpawnType = SpawnSpellType.SpawnOnMousePosition
            };
            entities.EventBus.RaiseEvent(EventSource.Local, rejected);
            Assert.That(rejected.Handled, Is.False);
            Assert.That(mana.Mana, Is.EqualTo(500));
            Assert.That(mana.CastedSpells, Is.Empty);

            entities.EventBus.RaiseEvent(EventSource.Local, new MedievalInstantSpawnEvent
            {
                Action = heal, Performer = mage, SpawnedEntityPrototype = "MedievalIceBarrierBeginner"
            });
            Assert.That(mana.Mana, Is.EqualTo(500));
            Assert.That(mana.CastedSpells, Is.Empty, "A denied final placement must release reserved mana.");
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(ent => ent.Comp.EntityPrototype?.ID == "MedievalIceBarrierBeginner"), Is.False);
            entities.EventBus.RaiseEvent(EventSource.Local, new MedievalInstantSpawnEvent
            {
                Action = heal, Performer = mage, SpawnedEntityPrototype = "HolyTouch"
            });
            Assert.That(mana.Mana, Is.EqualTo(500 - entities.GetComponent<ManaDrainSpellComponent>(heal).ManaDrain),
                "Allowed spells retain their normal mana cost.");
            mana.Mana = 0;
            entities.EventBus.RaiseEvent(EventSource.Local, new MedievalInstantSpawnEvent
            {
                Action = heal, Performer = mage, SpawnedEntityPrototype = "HolyTouch"
            });
            Assert.That(entities.GetComponent<DamageableComponent>(mage).TotalDamage, Is.GreaterThan(FixedPoint2.Zero),
                "Casting without mana must still pay the spell's health cost.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RealBombProtectsPartyAfterThrownOrExplicitlyTriggered(bool thrown)
    {
        var (server,arena,mage,tank)=await Setup();
        float before=0;
        await server.WaitAssertion(()=>
        {
            var e=server.EntMan;before=e.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float();
            var bomb=e.SpawnEntity("MedievalGrenade",new EntityCoordinates(arena.Comp.CombatGrid,.5f,.5f));
            if(thrown)
            {
                var ev=new Content.Shared.Throwing.ThrownEvent(mage,bomb);
                e.EventBus.RaiseLocalEvent(bomb,ref ev);
            }
            server.System<ExplosionSystem>().TriggerExplosive(bomb,user:thrown?null:mage);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(()=>
        {
            var e=server.EntMan;
            Assert.That(e.GetComponent<DamageableComponent>(arena.Comp.Boss).TotalDamage.Float(),Is.GreaterThan(before));
            foreach(var body in new[]{mage,tank})
            {
                Assert.That(e.GetComponent<DamageableComponent>(body).TotalDamage,Is.EqualTo(FixedPoint2.Zero));
                Assert.That(e.GetComponent<FlammableComponent>(body).OnFire,Is.False);
                Assert.That(e.GetComponent<PhysicsComponent>(body).LinearVelocity.Length(),Is.LessThan(.01f));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RealBombStillHurtsPartyWhenFromBossOrOutsideArena(bool outsideArena)
    {
        var (server,arena,mage,tank)=await Setup();
        await server.WaitAssertion(()=>
        {
            var e=server.EntMan;
            if(outsideArena)e.RemoveComponent<BossArenaRulesComponent>(arena.Owner);
            var bomb=e.SpawnEntity("MedievalGrenade",new EntityCoordinates(arena.Comp.CombatGrid,.5f,.5f));
            server.System<ExplosionSystem>().TriggerExplosive(bomb,user:outsideArena?mage:arena.Comp.Boss);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(()=>Assert.That(server.EntMan.GetComponent<DamageableComponent>(tank).TotalDamage.Float(),
            Is.GreaterThan(0),"Arena party protection must preserve hostile and ordinary explosions."));
    }

    private async Task<(ServerIntegrationInstance Server, Entity<BossArenaMapComponent> Arena, EntityUid Mage, EntityUid Tank)> Setup()
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
        Entity<BossArenaMapComponent> arena = default;
        EntityUid mage = default, tank = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"),
                out var map, out _, new DeserializationOptions { InitializeMaps = true }), Is.True);
            arena = new(map!.Value.Owner, server.EntMan.GetComponent<BossArenaMapComponent>(map.Value.Owner));
            server.System<SharedMapSystem>().SetPaused(map.Value.Comp.MapId, false);
            mage = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            tank = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, -0.5f, 0.5f));
            server.EntMan.RemoveComponent<Content.Shared.Imperial.Medieval.Additions.ShieldOnStartupComponent>(mage);
            server.EntMan.RemoveComponent<Content.Shared.Imperial.Medieval.Additions.ShieldOnStartupComponent>(tank);
            server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(mage);
            server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(tank);
            server.EntMan.DeleteEntity(arena.Comp.Crystal!.Value);
            arena.Comp.Crystal = null;
            arena.Comp.Boss = server.EntMan.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
        });
        return (server, arena, mage, tank);
    }

    private static DamageSpecifier Spec(string type, float amount)
        => new() { DamageDict = { [type] = FixedPoint2.New(amount) } };
}
