using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Medieval.BookAbilities;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Server.MagicPotionsMaker;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.SpikeTrap.Components;
using Content.Shared.ActionBlocker;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookPortableTest
{
    private static readonly ProtoId<TagPrototype> InstantWork = "InstantDoAfters";

    [Test]
    public async Task AnUntrainedCourierUnpacksTheSameCrateWithItsOriginalContents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var crate = entities.SpawnEntity("CrateGenericSteel", map.GridCoords);
            var payload = entities.SpawnEntity("MedievalWeaponCrossbow", map.GridCoords);
            var weight = entities.System<RDWeightSystem>();
            var emptyCrateWeight = weight.GetTotal(crate, refresh: true);
            var hadCrateWeight = entities.HasComponent<RDWeightComponent>(crate);
            var payloadWeightComponent = entities.GetComponent<RDWeightComponent>(payload);
            var payloadWeight = weight.GetTotal(payload, refresh: true);
            Assert.That(payloadWeight, Is.GreaterThan(0));
            var contents = entities.GetComponent<EntityStorageComponent>(crate).Contents;
            var containers = entities.System<SharedContainerSystem>();
            Assert.That(containers.Insert(payload, contents), Is.True);
            var originalLoadedWeight = weight.GetTotal(crate, refresh: true);
            entities.System<TagSystem>().AddTag(actor, InstantWork);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(actor, "BookPorter");
            var actions = entities.System<SharedActionsSystem>();
            var action = FindAction(entities, actor, "ActionBookPorter");
            var pack = new BookPorterActionEvent { Target = crate };
            actions.PerformAction(actor, action, pack);

            Assert.That(pack.Handled, Is.True);
            Assert.That(containers.TryGetContainingContainer(crate, out var packed), Is.True);
            var bundle = packed!.Owner;
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MetaDataComponent>(bundle).EntityPrototype!.ID, Is.EqualTo("MedievalBookPackedCrate"));
                Assert.That(packed.ContainedEntities, Is.EquivalentTo(new[] { crate }));
                Assert.That(contents.ContainedEntities, Is.EquivalentTo(new[] { payload }));
                Assert.That(entities.System<SharedHandsSystem>().IsHolding(actor, bundle, out _), Is.True);
                Assert.That(weight.GetTotal(crate, refresh: true), Is.EqualTo(emptyCrateWeight + payloadWeight).Within(0.001f));
                Assert.That(weight.GetTotal(bundle, refresh: true), Is.EqualTo(emptyCrateWeight + payloadWeight + 2f).Within(0.001f),
                    "Packing adds only its wrapper's mass; it must not copy or lose the original contents' weight.");
            });

            var courier = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.System<TagSystem>().AddTag(courier, InstantWork);
            Assert.That(entities.System<MedievalKnowledgeSystem>().HasKnowledge(courier, "BookPorter"), Is.False);
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryDrop(actor, bundle), Is.True);
            Assert.That(hands.TryPickupAnyHand(courier, bundle), Is.True);
            var unpack = new UseInHandEvent(courier);
            entities.EventBus.RaiseLocalEvent(bundle, unpack);
            Assert.Multiple(() =>
            {
                Assert.That(unpack.Handled, Is.True);
                Assert.That(containers.TryGetContainingContainer(crate, out _), Is.False);
                Assert.That(entities.GetComponent<EntityStorageComponent>(crate).Contents, Is.SameAs(contents));
                Assert.That(contents.ContainedEntities, Is.EquivalentTo(new[] { payload }));
                Assert.That(entities.GetComponent<MetaDataComponent>(crate).EntityPrototype!.ID, Is.EqualTo("CrateGenericSteel"));
                Assert.That(weight.GetTotal(crate, refresh: true), Is.EqualTo(originalLoadedWeight).Within(0.001f));
                Assert.That(entities.HasComponent<RDWeightComponent>(crate), Is.EqualTo(hadCrateWeight),
                    "Unpacking must restore the original component set.");
                Assert.That(entities.GetComponent<RDWeightComponent>(payload), Is.SameAs(payloadWeightComponent));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PackingARealWorkbenchSlowsItsCarrierThroughTheNativeMovementSystem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var station = entities.SpawnEntity("MedievalWoodWorkerPlace", map.GridCoords);
            entities.System<TagSystem>().AddTag(actor, InstantWork);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(actor, "BookPackWorkbench");
            var movement = entities.System<MovementSpeedModifierSystem>();
            movement.RefreshMovementSpeedModifiers(actor);
            var speed = entities.GetComponent<MovementSpeedModifierComponent>(actor);
            var previousWalk = speed.CurrentWalkSpeed;
            var previousSprint = speed.CurrentSprintSpeed;
            var actions = entities.System<SharedActionsSystem>();
            actions.PerformAction(actor, FindAction(entities, actor, "ActionBookPackWorkbench"),
                new BookPackWorkbenchActionEvent { Target = station });

            Assert.That(entities.System<SharedContainerSystem>().TryGetContainingContainer(station, out var packed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MetaDataComponent>(packed!.Owner).EntityPrototype!.ID, Is.EqualTo("MedievalBookPackedWorkbench"));
                Assert.That(packed!.ContainedEntities, Is.EquivalentTo(new[] { station }));
                Assert.That(entities.System<SharedHandsSystem>().IsHolding(actor, packed.Owner, out _), Is.True);
                Assert.That(speed.CurrentWalkSpeed, Is.LessThan(previousWalk));
                Assert.That(speed.CurrentSprintSpeed, Is.LessThan(previousSprint));
            });
            // The packed load's mass must slow the carrier even without its fixed speed penalty.
            entities.RemoveComponent<HeldSpeedModifierComponent>(packed!.Owner);
            movement.RefreshMovementSpeedModifiers(actor);
            Assert.That(speed.CurrentWalkSpeed, Is.LessThan(previousWalk));
            Assert.That(speed.CurrentSprintSpeed, Is.LessThan(previousSprint));
            Assert.That(entities.System<SharedHandsSystem>().TryDrop(actor, packed.Owner), Is.True);
            movement.RefreshMovementSpeedModifiers(actor);
            Assert.That(speed.CurrentWalkSpeed, Is.EqualTo(previousWalk).Within(0.001f));
            Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(previousSprint).Within(0.001f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WorkshopFamiliesAndVariantsKeepTheirIdentityStateAndFacingAfterUnpacking()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.System<TagSystem>().AddTag(actor, InstantWork);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(actor, "BookPackWorkbench");
            var actions = entities.System<SharedActionsSystem>();
            var action = FindAction(entities, actor, "ActionBookPackWorkbench");
            var transform = entities.System<SharedTransformSystem>();
            var containers = entities.System<SharedContainerSystem>();
            var weight = entities.System<RDWeightSystem>();
            // These use several independent crafting implementations, not just CP14Workbench.
            string[] workshops =
            [
                "MedievalWoodWorkerPlace", "MedievalClothWorkingPlace", "MedievalBlacksmithArmor",
                "MedievalLeatherWorkingPlace", "MedievalKeybench", "MedievalFoundry", "MedievalFoundryMines",
                "MedievalWizardBlacksmith", "MedievalStoneWorkerPlace", "MedievalStoneWizardPlace",
                "MedievalWizardPlace", "MedievalWizardPotion", "MedievalWizardGrass", "MedievalAlcPlace",
                "MedievalMortar", "Medievalalchem_dispenser", "MedievalCampfire", "MedievalMeltingPlace",
                "MedievalDecorKotelForCoockingPotions", "MedievalSmeltery", "MedievalSteelWorkbench",
                "MedievalSteelWeaponery", "MedievalAutolathe", "MedievalMyrmexRecycler",
                "MedievalMyrmexIncubator", "MedievalMyrmexSoupPlace"
            ];
            foreach (var prototype in workshops)
            {
                var station = entities.SpawnEntity(prototype, map.GridCoords);
                transform.SetWorldRotation(station, Angle.FromDegrees(90));
                // Some older lathes intentionally prohibit rotation (Transform.noRot).
                var facing = transform.GetWorldRotation(station);
                var anchored = entities.GetComponent<TransformComponent>(station).Anchored;
                var originalWeight = weight.GetTotal(station, refresh: true);
                var hadWeight = entities.HasComponent<RDWeightComponent>(station);
                var damage = entities.GetComponent<DamageableComponent>(station);
                entities.System<DamageableSystem>().TryChangeDamage(station,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, ignoreResistances: true);
                var initialDamage = damage.TotalDamage;
                Assert.That(actions.ValidateEntityTarget(actor, station,
                    (action, entities.GetComponent<EntityTargetActionComponent>(action))), Is.True, prototype);
                var pack = new BookPackWorkbenchActionEvent { Target = station };
                actions.PerformAction(actor, action, pack);
                Assert.That(pack.Handled, Is.True, prototype);
                Assert.That(containers.TryGetContainingContainer(station, out var packed), Is.True, prototype);
                var bundle = packed!.Owner;
                var packedObjectWeight = weight.GetTotal(station, refresh: true);
                Assert.That(weight.GetTotal(bundle, refresh: true), Is.EqualTo(packedObjectWeight + 42f).Within(0.001f), prototype);
                // Turning while carrying it used to rotate the unfolded station.
                transform.SetWorldRotation(actor, Angle.FromDegrees(180));
                var unpack = new ActivateInWorldEvent(actor, bundle, true);
                entities.EventBus.RaiseLocalEvent(bundle, unpack);
                Assert.Multiple(() =>
                {
                    Assert.That(unpack.Handled, Is.True, prototype);
                    Assert.That(containers.TryGetContainingContainer(station, out _), Is.False, prototype);
                    Assert.That(transform.GetWorldRotation(station), Is.EqualTo(facing), prototype);
                    Assert.That(entities.GetComponent<TransformComponent>(station).Anchored, Is.EqualTo(anchored), prototype);
                    Assert.That(entities.GetComponent<DamageableComponent>(station), Is.SameAs(damage), prototype);
                    Assert.That(damage.TotalDamage, Is.EqualTo(initialDamage), prototype);
                    Assert.That(weight.GetTotal(station, refresh: true), Is.EqualTo(originalWeight).Within(0.001f), prototype);
                    Assert.That(entities.HasComponent<RDWeightComponent>(station), Is.EqualTo(hadWeight), prototype);
                });
                entities.DeleteEntity(bundle);
                entities.DeleteEntity(station);
            }
            var wall = entities.SpawnEntity("WallSolid", map.GridCoords);
            Assert.That(actions.ValidateEntityTarget(actor, wall,
                (action, entities.GetComponent<EntityTargetActionComponent>(action))), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task VisibleSpikesDisarmAndReassembleTheRealTrap(bool active)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var trapEntity = entities.SpawnEntity("MedievalSpikePoint", map.GridCoords);
            var trap = entities.GetComponent<SpikeTrapComponent>(trapEntity);
            var traps = entities.System<SpikeTrapSystem>();
            if (active)
            {
                var victim = entities.SpawnEntity("MobHuman", map.GridCoords);
                entities.EnsureComponent<MedievalSpikeTargetComponent>(victim);
                trap.EndTime = pair.Server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(1);
                traps.Update(0.01f);
                entities.DeleteEntity(victim);
            }
            var visible = (active ? trap.ActiveTrapEntity : trap.DeactiveTrapEntity)!.Value;
            Assert.That(entities.HasComponent<SpikeTrapVisualComponent>(visible), Is.True);
            var otherTrapEntity = entities.SpawnEntity("MedievalSpikePoint", map.GridCoords);
            var otherTrap = entities.GetComponent<SpikeTrapComponent>(otherTrapEntity);
            var actor = entities.SpawnEntity("MobHuman",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(0.9f, 0)));
            entities.System<TagSystem>().AddTag(actor, InstantWork);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(actor, "BookTrapDisarm");
            var actions = entities.System<SharedActionsSystem>();
            var action = FindAction(entities, actor, "ActionBookTrapDisarm");
            Assert.That(actions.ValidateEntityTarget(actor, visible,
                (action, entities.GetComponent<EntityTargetActionComponent>(action))), Is.True);
            var disarm = new BookTrapDisarmActionEvent { Target = visible };
            actions.PerformAction(actor, action, disarm);
            Assert.That(disarm.Handled, Is.True);
            var containers = entities.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainingContainer(trapEntity, out var packed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(trap.Enabled, Is.False);
                Assert.That(trap.ActiveTrapEntity, Is.Null);
                Assert.That(trap.DeactiveTrapEntity, Is.Null);
                Assert.That(packed!.ContainedEntities, Is.EquivalentTo(new[] { trapEntity }));
                Assert.That(otherTrap.Enabled, Is.True, "Overlapping traps must resolve by the selected visual's exact UID.");
                Assert.That(containers.TryGetContainingContainer(otherTrapEntity, out _), Is.False);
            });
            var unpack = new UseInHandEvent(actor);
            entities.EventBus.RaiseLocalEvent(packed!.Owner, unpack);
            Assert.That(unpack.Handled, Is.True);
            Assert.That(containers.TryGetContainingContainer(trapEntity, out _), Is.False);
            Assert.That(trap.Enabled, Is.True);
            Assert.That(trap.Ready, Is.False, "Installing the trap gives the carrier a short safe departure period.");
            traps.Update(2.1f);
            Assert.That(trap.Ready, Is.True);
            var newVisual = trap.DeactiveTrapEntity!.Value;
            Assert.That(newVisual, Is.Not.EqualTo(visible));
            Assert.That(entities.HasComponent<SpikeTrapVisualComponent>(newVisual), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(newVisual).Coordinates,
                Is.EqualTo(entities.GetComponent<TransformComponent>(trapEntity).Coordinates));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredatorCannotAttackOrMoveDuringActualTrainingTicks()
    {
        await using var pair = await BookTestServer.Create();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitPost(() =>
        {
            // Vacuum damage correctly interrupts training, so use an ordinary breathable environment.
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = Atmospherics.OxygenMolesStandard;
            moles[(int) Gas.Nitrogen] = Atmospherics.NitrogenMolesStandard;
            entities.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false,
                new GasMixture(moles, Atmospherics.T20C));
        });
        await pair.RunTicksSync(10);
        EntityUid actor = default;
        EntityUid beast = default;
        FixedPoint2 initialDamage = default;
        FixedPoint2 initialBeastDamage = default;
        Content.Shared.DoAfter.DoAfter channel = default!;
        await pair.Server.WaitAssertion(() =>
        {
            actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            beast = entities.SpawnEntity("MedievalMobBear",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(0.9f, 0)));
            PrepareTaming(entities, actor, beast);
            initialDamage = entities.GetComponent<DamageableComponent>(actor).TotalDamage;
            initialBeastDamage = entities.GetComponent<DamageableComponent>(beast).TotalDamage;
            entities.System<SharedActionsSystem>().PerformAction(actor, FindAction(entities, actor, "ActionBookTaming"),
                new BookTamingActionEvent { Target = beast });
            Assert.That(entities.HasComponent<BookTamingLockComponent>(actor), Is.True);
            channel = entities.GetComponent<DoAfterComponent>(actor).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed);
        });
        await pair.RunTicksSync(120);
        await pair.Server.WaitAssertion(() =>
        {
            var blocker = entities.System<ActionBlockerSystem>();
            var beastDamage = entities.GetComponent<DamageableComponent>(beast);
            Assert.Multiple(() =>
            {
                Assert.That(channel.Cancelled, Is.False);
                Assert.That(channel.Completed, Is.False, "This is a 30-second channel, still running after 120 ticks.");
                Assert.That(entities.GetComponent<DamageableComponent>(actor).TotalDamage, Is.EqualTo(initialDamage));
                Assert.That(beastDamage.TotalDamage, Is.EqualTo(initialBeastDamage));
                Assert.That(blocker.CanMove(actor), Is.False);
                Assert.That(blocker.CanMove(beast), Is.False);
                Assert.That(entities.GetComponent<HTNComponent>(beast).Enabled, Is.False);
                Assert.That(entities.HasComponent<NPCMeleeCombatComponent>(beast), Is.False);
            });
            entities.System<SharedActionsSystem>().PerformAction(actor, FindAction(entities, actor, "ActionBookCancelTaming"));
            Assert.That(blocker.CanMove(actor), Is.True);
            Assert.That(blocker.CanMove(beast), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("action")]
    [TestCase("movement")]
    [TestCase("damage")]
    public async Task InterruptingTamingReleasesBothAndRetaliatesWithoutRefundingCooldown(string interruption)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var beast = entities.SpawnEntity("MedievalMobBear", map.GridCoords);
            PrepareTaming(entities, actor, beast);
            var actions = entities.System<SharedActionsSystem>();
            var taming = FindAction(entities, actor, "ActionBookTaming");
            actions.PerformAction(actor, taming, new BookTamingActionEvent { Target = beast });
            var cooldown = taming.Comp.Cooldown;
            var blocker = entities.System<ActionBlockerSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(blocker.CanMove(actor), Is.False);
                Assert.That(blocker.CanMove(beast), Is.False);
                Assert.That(entities.GetComponent<HTNComponent>(beast).Enabled, Is.False);
                Assert.That(cooldown, Is.Not.Null);
                Assert.That(actions.IsCooldownActive(taming.Comp), Is.True);
            });

            switch (interruption)
            {
                case "action":
                    actions.PerformAction(actor, FindAction(entities, actor, "ActionBookCancelTaming"));
                    break;
                case "movement":
                    var mover = entities.GetComponent<InputMoverComponent>(actor);
                    var input = new MoveInputEvent((actor, mover), mover.HeldMoveButtons);
                    mover.HeldMoveButtons = MoveButtons.Up;
                    entities.EventBus.RaiseLocalEvent(actor, ref input);
                    break;
                case "damage":
                    entities.System<DamageableSystem>().TryChangeDamage(beast,
                        new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, ignoreResistances: true);
                    break;
            }

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<BookTamingLockComponent>(actor), Is.False);
                Assert.That(entities.HasComponent<BookTamingLockComponent>(beast), Is.False);
                Assert.That(blocker.CanMove(actor), Is.True);
                Assert.That(blocker.CanMove(beast), Is.True);
                Assert.That(entities.GetComponent<HTNComponent>(beast).Enabled, Is.True);
                Assert.That(entities.GetComponent<FactionExceptionComponent>(beast).Hostiles, Does.Contain(actor));
                Assert.That(entities.GetComponent<NPCMeleeCombatComponent>(beast).Target, Is.EqualTo(actor));
                Assert.That(actions.GetActions(actor).Any(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "ActionBookCancelTaming"), Is.False);
                Assert.That(entities.GetComponent<DoAfterComponent>(actor).DoAfters.Values.Any(d => !d.Cancelled && !d.Completed), Is.False);
                Assert.That(taming.Comp.Cooldown, Is.EqualTo(cooldown));
                Assert.That(actions.IsCooldownActive(taming.Comp), Is.True);
            });
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DeletingEitherTrainingParticipantNeverLeavesTheSurvivorFrozen(bool deleteTrainer)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var beast = entities.SpawnEntity("MedievalMobBear", map.GridCoords);
            PrepareTaming(entities, actor, beast);
            entities.System<SharedActionsSystem>().PerformAction(actor, FindAction(entities, actor, "ActionBookTaming"),
                new BookTamingActionEvent { Target = beast });
            Assert.That(entities.HasComponent<BookTamingLockComponent>(actor), Is.True);
            Assert.That(entities.HasComponent<BookTamingLockComponent>(beast), Is.True);

            entities.DeleteEntity(deleteTrainer ? actor : beast);
            var survivor = deleteTrainer ? beast : actor;
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<BookTamingLockComponent>(survivor), Is.False);
                Assert.That(entities.System<ActionBlockerSystem>().CanMove(survivor), Is.True);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImmediatelyCompletedTrainingKeepsTheCreatureAndLeavesNoTemporaryLocksOrActions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var beast = entities.SpawnEntity("MedievalMobBear", map.GridCoords);
            PrepareTaming(entities, actor, beast);
            entities.System<TagSystem>().AddTag(actor, InstantWork);
            var originalDamage = entities.GetComponent<DamageableComponent>(beast);
            var actions = entities.System<SharedActionsSystem>();
            actions.PerformAction(actor, FindAction(entities, actor, "ActionBookTaming"), new BookTamingActionEvent { Target = beast });
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MedievalCompanionComponent>(beast).Master, Is.EqualTo(actor));
                Assert.That(entities.GetComponent<DamageableComponent>(beast), Is.SameAs(originalDamage));
                Assert.That(entities.HasComponent<BookTamingLockComponent>(actor), Is.False);
                Assert.That(entities.HasComponent<BookTamingLockComponent>(beast), Is.False);
                Assert.That(actions.GetActions(actor).Any(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "ActionBookCancelTaming"), Is.False);
                Assert.That(actions.IsCooldownActive(FindAction(entities, actor, "ActionBookTaming").Comp), Is.True);
            });
        });
        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> FindAction(IEntityManager entities, EntityUid actor, string prototype) =>
        entities.System<SharedActionsSystem>().GetActions(actor)
            .Single(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == prototype);

    private static void PrepareTaming(IEntityManager entities, EntityUid actor, EntityUid beast)
    {
        var hands = entities.System<SharedHandsSystem>();
        var die = entities.SpawnEntity("d6Dice", entities.GetComponent<TransformComponent>(actor).Coordinates);
        Assert.That(hands.TryPickupAnyHand(actor, die), Is.True);
        var food = entities.SpawnEntity("FoodMeat", entities.GetComponent<TransformComponent>(actor).Coordinates);
        Assert.That(hands.TryPickupAnyHand(actor, food), Is.True);
        Assert.That(hands.IsHolding(actor, die, out var dieHand), Is.True);
        hands.SetActiveHand(actor, dieHand);
        Assert.That(hands.GetActiveItem(actor), Is.EqualTo(die), "Food must be found in the other hand.");
        entities.System<DamageableSystem>().TryChangeDamage(beast,
            new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } }, ignoreResistances: true);
        entities.System<MedievalKnowledgeSystem>().GrantKnowledge(actor, "BookTaming");
    }
}
