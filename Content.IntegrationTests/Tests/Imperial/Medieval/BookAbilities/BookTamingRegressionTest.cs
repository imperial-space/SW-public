using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Imperial.Medieval.BookAbilities;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookTamingRegressionTest
{
    private BookTestServer _pair = default!;

    [OneTimeSetUp]
    public async Task StartServer() => _pair = await BookTestServer.Create();

    [TearDown]
    public Task ClearEntities() => _pair.Server.WaitPost(() => _pair.Server.EntMan.FlushEntities());

    [OneTimeTearDown]
    public async Task StopServer() => await _pair.CleanReturnAsync();

    [Test]
    public async Task WolfTrainingSurvivesCollisionSeparationAndCompletes()
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await MakeBreathable(pair, map);
        EntityUid trainer = default;
        EntityUid wolf = default;
        EntityUid food = default;
        Content.Shared.DoAfter.DoAfter channel = default!;
        await pair.Server.WaitAssertion(() =>
        {
            trainer = entities.SpawnEntity("MobHuman", map.GridCoords);
            wolf = entities.SpawnEntity("MedievalMobWolf", map.GridCoords);
            food = Prepare(entities, trainer, wolf);
            entities.EnsureComponent<NPCMeleeCombatComponent>(wolf).Target = trainer;
            entities.System<SharedPhysicsSystem>().SetLinearVelocity(wolf, new Vector2(3, 0));
            Start(entities, trainer, wolf);
            channel = entities.GetComponent<DoAfterComponent>(trainer).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed);
            // Model a small collision/network position correction while the wolf stays within reach.
            entities.System<SharedTransformSystem>().SetCoordinates(wolf, new EntityCoordinates(map.Grid, new Vector2(0.4f, 0)));
        });
        // An approaching wolf can overlap the trainer. Collision resolution must not cancel the channel.
        await pair.RunTicksSync(120);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(channel.Cancelled, Is.False, "Physical separation of overlapping bodies is not a voluntary interruption.");
            Assert.That(channel.Completed, Is.False);
            Assert.That(entities.GetComponent<HTNComponent>(wolf).Enabled, Is.False);
            Assert.That(entities.HasComponent<NPCMeleeCombatComponent>(wolf), Is.False);
            Assert.That(entities.System<MedievalCompanionSystem>().OwnedCount(trainer), Is.Zero);
            Assert.That(entities.System<SharedHandsSystem>().IsHolding(trainer, food), Is.True);
            Assert.That(entities.System<ActionBlockerSystem>().CanAttack(wolf, trainer), Is.False);
            Assert.That(entities.System<ActionBlockerSystem>().CanAttack(trainer, wolf), Is.False);
            Assert.That(entities.GetComponent<DamageableComponent>(trainer).TotalDamage, Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero));
            entities.GetComponent<MedievalPacifiedBeastComponent>(wolf).Until = TimeSpan.Zero;
        });
        await pair.RunTicksSync(60);
        await pair.Server.WaitAssertion(() =>
            Assert.That(entities.GetComponent<HTNComponent>(wolf).Enabled, Is.False,
                "The training lock, not a separate pacification timer, controls when the wolf resumes its AI."));
        await pair.RunTicksSync(840);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(channel.Cancelled, Is.False);
            Assert.That(channel.Completed, Is.True);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(wolf).Master, Is.EqualTo(trainer));
            Assert.That(entities.System<MedievalCompanionSystem>().OwnedCount(trainer), Is.EqualTo(1));
            Assert.That(entities.Deleted(food), Is.True);
            AssertReleased(entities, trainer, wolf);
        });
    }

    [Test]
    public async Task DistantMovingWolfCanBeTamedToCompletion()
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await MakeBreathable(pair, map);
        EntityUid trainer = default;
        EntityUid wolf = default;
        EntityUid food = default;
        Content.Shared.DoAfter.DoAfter channel = default!;
        await pair.Server.WaitAssertion(() =>
        {
            AddRangeTestFloor(map);
            trainer = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            wolf = entities.SpawnEntity("MedievalMobWolf", new EntityCoordinates(map.Grid, new Vector2(6, 0.5f)));
            food = Prepare(entities, trainer, wolf);
            var action = FindAction(entities, trainer, "ActionBookTaming");
            Assert.That(entities.System<SharedActionsSystem>().ValidateEntityTarget(trainer, wolf,
                (action.Owner, entities.GetComponent<EntityTargetActionComponent>(action))), Is.True,
                "The action's target selection must accept the same range as the server channel.");
            entities.System<SharedPhysicsSystem>().SetLinearVelocity(wolf, new Vector2(3, 0));
            Start(entities, trainer, wolf);
            channel = entities.GetComponent<DoAfterComponent>(trainer).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed);
        });
        await pair.RunTicksSync(120);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(channel.Cancelled, Is.False, "The timer must not keep the old melee-distance limit.");
            Assert.That(entities.GetComponent<HTNComponent>(wolf).Enabled, Is.False);
            Assert.That(entities.System<ActionBlockerSystem>().CanMove(wolf), Is.False);
        });
        await pair.RunTicksSync(900);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(channel.Cancelled, Is.False);
            Assert.That(channel.Completed, Is.True);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(wolf).Master, Is.EqualTo(trainer));
            Assert.That(entities.Deleted(food), Is.True);
            AssertReleased(entities, trainer, wolf);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task TamingRejectsWallsAndTargetsBeyondSixTiles(bool wall)
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            AddRangeTestFloor(map);
            var trainer = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            var wolf = entities.SpawnEntity("MedievalMobWolf", new EntityCoordinates(map.Grid, new Vector2(wall ? 6 : 8, 0.5f)));
            var food = Prepare(entities, trainer, wolf);
            if (wall)
                entities.SpawnEntity("MedievalWallStoneAncient", new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f)));
            var action = FindAction(entities, trainer, "ActionBookTaming");
            var actions = entities.System<SharedActionsSystem>();
            Assert.That(actions.ValidateEntityTarget(trainer, wolf,
                (action.Owner, entities.GetComponent<EntityTargetActionComponent>(action))), Is.False);
            // Bypass target selection to verify the server also rejects an invalid request.
            Start(entities, trainer, wolf);
            AssertReleased(entities, trainer, wolf);
            Assert.That(actions.IsCooldownActive(action.Comp), Is.False);
            Assert.That(entities.System<SharedHandsSystem>().IsHolding(trainer, food), Is.True);
        });
    }

    private void AddRangeTestFloor(TestMapData map)
    {
        var tileId = _pair.Server.Resolve<ITileDefinitionManager>()["Plating"].TileId;
        var maps = _pair.Server.System<SharedMapSystem>();
        for (var x = -1; x <= 9; x++)
        for (var y = -1; y <= 1; y++)
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), new Tile(tileId));
    }

    [TestCase(true, "bloodloss", false)]
    [TestCase(false, "bloodloss", false)]
    [TestCase(true, "poison", false)]
    [TestCase(false, "poison", false)]
    [TestCase(true, "legacy-disease", true)]
    [TestCase(false, "legacy-disease", true)]
    public async Task DamageFollowsNativeDoAfterRules(bool damageWolf, string source, bool interrupts)
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await MakeBreathable(pair, map);
        EntityUid trainer = default;
        EntityUid wolf = default;
        EntityUid food = default;
        EntityUid damaged = default;
        Content.Shared.DoAfter.DoAfter channel = default!;
        await pair.Server.WaitAssertion(() =>
        {
            trainer = entities.SpawnEntity("MobHuman", map.GridCoords);
            wolf = entities.SpawnEntity("MedievalMobWolf", new EntityCoordinates(map.Grid, new Vector2(0.9f, 0)));
            food = Prepare(entities, trainer, wolf);
            damaged = damageWolf ? wolf : trainer;
            if (source == "bloodloss")
            {
                var blood = entities.GetComponent<BloodstreamComponent>(damaged);
                var missingBlood = blood.BloodMaxVolume * (1f - blood.BloodlossThreshold + 0.1f);
                Assert.That(entities.System<SharedBloodstreamSystem>().TryModifyBloodLevel(damaged, -missingBlood), Is.True);
            }
            Start(entities, trainer, wolf);
            channel = entities.GetComponent<DoAfterComponent>(trainer).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed);
            if (source != "bloodloss")
            {
                var before = entities.GetComponent<DamageableComponent>(damaged).TotalDamage;
                var damage = new DamageSpecifier { DamageDict = { ["Poison"] = 5 } };
                if (source == "poison")
                    new HealthChange { Damage = damage }.Effect(new EntityEffectBaseArgs(damaged, entities));
                else
                    // Legacy disease effects leave InterruptsDoAfters at its default true.
                    entities.System<DamageableSystem>().TryChangeDamage(damaged, damage, ignoreResistances: true);
                Assert.That(entities.GetComponent<DamageableComponent>(damaged).TotalDamage.Float(), Is.GreaterThan(before.Float()),
                    "Training must not prevent damage; it only ignores damage as an interruption condition.");
            }
        });
        await pair.RunTicksSync(120);
        await pair.Server.WaitAssertion(() =>
        {
            if (source == "bloodloss")
                Assert.That(entities.GetComponent<DamageableComponent>(damaged).Damage.DamageDict["Bloodloss"].Float(), Is.GreaterThan(0),
                    "Exercise the actual bloodstream tick, not just a synthetic event.");
            Assert.That(channel.Cancelled, Is.EqualTo(interrupts), "Use the damage source's native interruption rule without special cases.");
            if (interrupts)
            {
                AssertReleased(entities, trainer, wolf);
                Assert.That(entities.Deleted(food), Is.False);
                Assert.That(entities.System<MedievalCompanionSystem>().OwnedCount(trainer), Is.Zero);
                return;
            }
            Assert.That(entities.HasComponent<BookTamingLockComponent>(trainer), Is.True);
            Assert.That(entities.GetComponent<HTNComponent>(wolf).Enabled, Is.False);
            Assert.That(entities.HasComponent<MedievalPacifiedBeastComponent>(wolf), Is.True);
        });
        if (interrupts)
            return;
        await pair.RunTicksSync(900);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(channel.Cancelled, Is.False);
            Assert.That(channel.Completed, Is.True);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(wolf).Master, Is.EqualTo(trainer));
            Assert.That(entities.Deleted(food), Is.True);
            AssertReleased(entities, trainer, wolf);
        });
    }

    [TestCase("melee", true)]
    [TestCase("melee", false)]
    [TestCase("arrow", true)]
    [TestCase("arrow", false)]
    [TestCase("spell", true)]
    [TestCase("spell", false)]
    [TestCase("attributed-damage", true)]
    [TestCase("attributed-damage", false)]
    [TestCase("explosion", true)]
    [TestCase("explosion", false)]
    public async Task DirectAttacksInterruptEitherParticipant(string attack, bool hitWolf)
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await MakeBreathable(pair, map);
        EntityUid trainer = default;
        EntityUid wolf = default;
        EntityUid food = default;
        EntityUid target = default;
        float damageBefore = 0;
        Entity<ActionComponent> action = default;
        await pair.Server.WaitAssertion(() =>
        {
            trainer = entities.SpawnEntity("MobHuman", map.GridCoords);
            wolf = entities.SpawnEntity("MedievalMobWolf", new EntityCoordinates(map.Grid, new Vector2(0.9f, 0)));
            food = Prepare(entities, trainer, wolf);
            action = FindAction(entities, trainer, "ActionBookTaming");
            Start(entities, trainer, wolf);
            target = hitWolf ? wolf : trainer;
            var position = entities.GetComponent<TransformComponent>(target).Coordinates;
            var attacker = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, position.Position + new Vector2(0, 1)));
            damageBefore = entities.GetComponent<DamageableComponent>(target).TotalDamage.Float();
            switch (attack)
            {
                case "melee":
                    entities.System<SharedCombatModeSystem>().SetInCombatMode(attacker, true);
                    Assert.That(entities.System<SharedMeleeWeaponSystem>().AttemptLightAttack(attacker, attacker,
                        entities.GetComponent<MeleeWeaponComponent>(attacker), target), Is.True);
                    break;
                case "arrow":
                case "spell":
                    var projectile = entities.SpawnEntity(attack == "arrow" ? "MedievalArrowBullet" : "MedievalProjectileMagicArrowBeginner",
                        new EntityCoordinates(map.Grid, position.Position + new Vector2(0, 0.65f)));
                    entities.System<GunSystem>().ShootProjectile(projectile, -Vector2.UnitY, Vector2.Zero, attacker, attacker, 8f);
                    break;
                case "attributed-damage":
                    entities.System<DamageableSystem>().TryChangeDamage(target,
                        new DamageSpecifier { DamageDict = { ["Heat"] = 5 } }, ignoreResistances: true, origin: attacker);
                    break;
                case "explosion":
                    entities.System<ExplosionSystem>().QueueExplosion(target, "Default", 4f, 1f, 2f,
                        maxTileBreak: 0, canCreateVacuum: false, user: attacker);
                    break;
            }
        });
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.GreaterThan(damageBefore),
                "The attack must actually reach its target.");
            AssertReleased(entities, trainer, wolf);
            Assert.That(entities.System<MedievalCompanionSystem>().OwnedCount(trainer), Is.Zero);
            Assert.That(entities.Deleted(food), Is.False);
            Assert.That(entities.System<SharedActionsSystem>().IsCooldownActive(action.Comp), Is.True);
        });
    }

    [TestCase("movement")]
    [TestCase("action")]
    [TestCase("distance")]
    [TestCase("food")]
    [TestCase("death")]
    public async Task FailedTrainingLeavesNoCompanionAndCanBeRetried(string cause)
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await MakeBreathable(pair, map);
        EntityUid trainer = default;
        EntityUid wolf = default;
        EntityUid food = default;
        Entity<ActionComponent> action = default;
        await pair.Server.WaitAssertion(() =>
        {
            trainer = entities.SpawnEntity("MobHuman", map.GridCoords);
            wolf = entities.SpawnEntity("MedievalMobWolf", new EntityCoordinates(map.Grid, new Vector2(0.9f, 0)));
            food = Prepare(entities, trainer, wolf);
            action = FindAction(entities, trainer, "ActionBookTaming");
            Start(entities, trainer, wolf);
            Assert.That(entities.HasComponent<BookTamingLockComponent>(trainer), Is.True);
            switch (cause)
            {
                case "movement":
                    var mover = entities.GetComponent<InputMoverComponent>(trainer);
                    var input = new MoveInputEvent((trainer, mover), mover.HeldMoveButtons);
                    mover.HeldMoveButtons = MoveButtons.Up;
                    entities.EventBus.RaiseLocalEvent(trainer, ref input);
                    mover.HeldMoveButtons = MoveButtons.None;
                    break;
                case "action":
                    entities.System<SharedActionsSystem>().PerformAction(trainer, FindAction(entities, trainer, "ActionBookCancelTaming"));
                    break;
                case "distance":
                    entities.System<SharedTransformSystem>().SetCoordinates(wolf, new EntityCoordinates(map.Grid, new Vector2(8, 0)));
                    break;
                case "food":
                    Assert.That(entities.System<SharedHandsSystem>().TryDrop(trainer, food), Is.True);
                    break;
                case "death":
                    entities.System<DamageableSystem>().TryChangeDamage(wolf,
                        new DamageSpecifier { DamageDict = { ["Blunt"] = 200 } }, ignoreResistances: true);
                    break;
            }
        });
        await pair.RunTicksSync(1);
        await pair.Server.WaitAssertion(() =>
        {
            var pets = entities.System<MedievalCompanionSystem>();
            Assert.That(pets.OwnedCount(trainer), Is.Zero);
            Assert.That(entities.HasComponent<MedievalCompanionComponent>(wolf), Is.False);
            Assert.That(entities.Deleted(food), Is.False, "Food is consumed only after successful training.");
            AssertReleased(entities, trainer, wolf);
            var actions = entities.System<SharedActionsSystem>();
            Assert.That(actions.IsCooldownActive(action.Comp), Is.True);
            // Skip only the already-checked cooldown; a new real channel must start with another wild wolf.
            actions.SetCooldown(action, TimeSpan.Zero, TimeSpan.Zero);
            entities.DeleteEntity(wolf);
            if (cause == "food")
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(trainer, food), Is.True);
            wolf = entities.SpawnEntity("MedievalMobWolf", new EntityCoordinates(map.Grid, new Vector2(0.9f, 0)));
            entities.System<DamageableSystem>().TryChangeDamage(wolf,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } }, ignoreResistances: true);
            Start(entities, trainer, wolf);
            Assert.That(entities.HasComponent<BookTamingLockComponent>(trainer), Is.True);
            Assert.That(pets.OwnedCount(trainer), Is.Zero);
            actions.PerformAction(trainer, FindAction(entities, trainer, "ActionBookCancelTaming"));
            AssertReleased(entities, trainer, wolf);
        });
    }

    [Test]
    public async Task DeadCompanionDoesNotPreventTrainingAnotherWolf()
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var trainer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var wolf = entities.SpawnEntity("MedievalMobWolf", map.GridCoords);
            var pets = entities.System<MedievalCompanionSystem>();
            Assert.That(pets.Tame(wolf, trainer), Is.True);
            entities.System<DamageableSystem>().TryChangeDamage(wolf,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 200 } }, ignoreResistances: true);
            Assert.That(entities.GetComponent<MobStateComponent>(wolf).CurrentState, Is.EqualTo(MobState.Dead));
            Assert.That(pets.OwnedCount(trainer), Is.Zero);
            var next = entities.SpawnEntity("MedievalMobWolf", map.GridCoords);
            Prepare(entities, trainer, next);
            Start(entities, trainer, next);
            Assert.That(entities.HasComponent<BookTamingLockComponent>(trainer), Is.True);
            entities.System<SharedActionsSystem>().PerformAction(trainer, FindAction(entities, trainer, "ActionBookCancelTaming"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DeletingEitherParticipantReleasesTheOther(bool deleteTrainer)
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var trainer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var wolf = entities.SpawnEntity("MedievalMobWolf", map.GridCoords);
            Prepare(entities, trainer, wolf);
            Start(entities, trainer, wolf);
            Assert.That(entities.HasComponent<BookTamingLockComponent>(trainer), Is.True);
            entities.DeleteEntity(deleteTrainer ? trainer : wolf);
            var survivor = deleteTrainer ? wolf : trainer;
            Assert.That(entities.HasComponent<BookTamingLockComponent>(survivor), Is.False);
            Assert.That(entities.System<ActionBlockerSystem>().CanMove(survivor), Is.True);
            Assert.That(entities.System<MedievalCompanionSystem>().OwnedCount(trainer), Is.Zero);
        });
    }

    [Test]
    public async Task ReleasingHeldMovementDoesNotCancelButANewDirectionDoes()
    {
        var pair = _pair;
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var trainer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var wolf = entities.SpawnEntity("MedievalMobWolf", map.GridCoords);
            Prepare(entities, trainer, wolf);
            var mover = entities.GetComponent<InputMoverComponent>(trainer);
            mover.HeldMoveButtons = MoveButtons.Up | MoveButtons.Right;
            Start(entities, trainer, wolf);
            var release = new MoveInputEvent((trainer, mover), mover.HeldMoveButtons);
            mover.HeldMoveButtons = MoveButtons.Up;
            entities.EventBus.RaiseLocalEvent(trainer, ref release);
            Assert.That(entities.HasComponent<BookTamingLockComponent>(trainer), Is.True);
            var press = new MoveInputEvent((trainer, mover), mover.HeldMoveButtons);
            mover.HeldMoveButtons |= MoveButtons.Left;
            entities.EventBus.RaiseLocalEvent(trainer, ref press);
            AssertReleased(entities, trainer, wolf);
            Assert.That(entities.System<MedievalCompanionSystem>().OwnedCount(trainer), Is.Zero);
        });
    }

    private static EntityUid Prepare(IEntityManager entities, EntityUid trainer, EntityUid wolf)
    {
        // A fresh MobHuman is immune to damage for 45 seconds. Test an ordinary vulnerable player.
        entities.RemoveComponent<ShieldOnStartupComponent>(trainer);
        var food = entities.SpawnEntity("FoodMeat", entities.GetComponent<TransformComponent>(trainer).Coordinates);
        Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(trainer, food), Is.True);
        entities.System<DamageableSystem>().TryChangeDamage(wolf,
            new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } }, ignoreResistances: true);
        entities.System<MedievalKnowledgeSystem>().GrantKnowledge(trainer, "BookTaming");
        return food;
    }

    private static void Start(IEntityManager entities, EntityUid trainer, EntityUid wolf) =>
        entities.System<SharedActionsSystem>().PerformAction(trainer, FindAction(entities, trainer, "ActionBookTaming"),
            new BookTamingActionEvent { Target = wolf });

    private static Entity<ActionComponent> FindAction(IEntityManager entities, EntityUid owner, string prototype) =>
        entities.System<SharedActionsSystem>().GetActions(owner)
            .Single(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == prototype);

    private static void AssertReleased(IEntityManager entities, EntityUid trainer, EntityUid wolf)
    {
        Assert.That(entities.HasComponent<BookTamingLockComponent>(trainer), Is.False);
        Assert.That(entities.HasComponent<BookTamingLockComponent>(wolf), Is.False);
        Assert.That(entities.HasComponent<MedievalPacifiedBeastComponent>(wolf), Is.False);
        Assert.That(entities.System<SharedActionsSystem>().GetActions(trainer)
            .Any(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "ActionBookCancelTaming"), Is.False);
    }

    private static Task MakeBreathable(BookTestServer pair, TestMapData map) => pair.Server.WaitPost(() =>
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = Atmospherics.OxygenMolesStandard;
        moles[(int) Gas.Nitrogen] = Atmospherics.NitrogenMolesStandard;
        pair.Server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false,
            new GasMixture(moles, Atmospherics.T20C));
    });
}
