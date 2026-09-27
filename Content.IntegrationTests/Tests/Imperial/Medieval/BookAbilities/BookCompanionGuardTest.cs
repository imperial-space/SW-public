using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gravity;
using Content.Server.Imperial.Medieval.BookAbilities;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookCompanionGuardTest
{
    private BookTestServer _pair = default!;

    [OneTimeSetUp]
    public async Task StartServer() => _pair = await BookTestServer.Create();

    [TearDown]
    public Task ClearEntities() => _pair.Server.WaitPost(() => _pair.Server.EntMan.FlushEntities());

    [OneTimeTearDown]
    public async Task StopServer() => await _pair.CleanReturnAsync();

    [Test]
    public async Task GuardFollowsTheSelectedMovingPersonInsteadOfTheOwner()
    {
        var scene = await CreateScene("MobHuman");
        var entities = _pair.Server.EntMan;
        await _pair.Server.WaitAssertion(() => Guard(scene.Owner, scene.Target));
        await Advance(360);
        await _pair.Server.WaitAssertion(() =>
        {
            AssertNear(scene.Wolf, scene.Target);
            var destination = entities.GetComponent<TransformComponent>(scene.Target).Coordinates.Offset(new Vector2(5, 0));
            entities.System<SharedTransformSystem>().SetCoordinates(scene.Target, destination);
        });
        await Advance(360);
        await _pair.Server.WaitAssertion(() =>
        {
            AssertNear(scene.Wolf, scene.Target);
            Assert.That(entities.GetComponent<TransformComponent>(scene.Wolf).Coordinates.TryDistance(entities,
                entities.GetComponent<TransformComponent>(scene.Owner).Coordinates, out var distance), Is.True);
            Assert.That(distance, Is.GreaterThan(6), "The companion must follow the chosen person, not its owner.");
        });
    }

    [TestCase("MobHuman")]
    [TestCase("MedievalChest")]
    public async Task GuardDefendsTheChosenTargetAndStopsChasingAwayFromIt(string prototype)
    {
        var scene = await CreateScene(prototype);
        var entities = _pair.Server.EntMan;
        EntityUid attacker = default;
        await _pair.Server.WaitAssertion(() =>
        {
            Guard(scene.Owner, scene.Target);
            var coordinates = entities.GetComponent<TransformComponent>(scene.Target).Coordinates;
            entities.System<SharedTransformSystem>().SetCoordinates(scene.Wolf, coordinates.Offset(new Vector2(-1, 0)));
            attacker = entities.SpawnEntity("MobHuman", coordinates.Offset(new Vector2(1, 0)));
            entities.RemoveComponent<ShieldOnStartupComponent>(attacker);
            // Attacking the owner must not redirect a companion assigned to protect someone else.
            Hit(scene.Owner, attacker);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(scene.Wolf).CommandedEnemy, Is.Null);
            Hit(scene.Target, attacker);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(scene.Wolf).CommandedEnemy, Is.EqualTo(attacker));
        });
        await Advance(180);
        await _pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<DamageableComponent>(attacker).TotalDamage.Float(), Is.GreaterThan(0),
                "The guarding wolf must actually attack the aggressor. " + Describe(scene.Wolf));
            var away = entities.GetComponent<TransformComponent>(scene.Target).Coordinates.Offset(new Vector2(8, 0));
            entities.System<SharedTransformSystem>().SetCoordinates(attacker, away);
        });
        await Advance(360);
        await _pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(scene.Wolf).CommandedEnemy, Is.Null);
            Assert.That(entities.System<NpcFactionSystem>().GetHostiles(scene.Wolf), Does.Not.Contain(attacker));
            AssertNear(scene.Wolf, scene.Target);
        });
    }

    [Test]
    public async Task GuardCanSelectTheOwnerAndSwitchToAnotherTarget()
    {
        var scene = await CreateScene("MobHuman");
        var entities = _pair.Server.EntMan;
        await _pair.Server.WaitAssertion(() =>
        {
            Guard(scene.Owner, scene.Owner);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(scene.Wolf).GuardTarget, Is.EqualTo(scene.Owner));
            var attacker = entities.SpawnEntity("MobHuman", entities.GetComponent<TransformComponent>(scene.Owner).Coordinates.Offset(new Vector2(1, 0)));
            Hit(scene.Owner, attacker);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(scene.Wolf).CommandedEnemy, Is.EqualTo(attacker));
            Guard(scene.Owner, scene.Target);
            Assert.That(entities.HasComponent<MedievalGuardedTargetComponent>(scene.Owner), Is.False);
            Hit(scene.Owner, attacker);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(scene.Wolf).CommandedEnemy, Is.Null);
            entities.System<MedievalCompanionSystem>().IssueOrder(scene.Wolf, "stay");
            Assert.That(entities.HasComponent<MedievalGuardedTargetComponent>(scene.Target), Is.False);
            Assert.That(entities.GetComponent<MedievalCompanionComponent>(scene.Wolf).GuardTarget, Is.Null);
            Assert.That(entities.GetComponent<HTNComponent>(scene.Wolf).Enabled, Is.False);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DeletionCleansUpGuarding(bool deleteTarget)
    {
        var scene = await CreateScene("MedievalChest");
        var entities = _pair.Server.EntMan;
        await _pair.Server.WaitAssertion(() =>
        {
            Guard(scene.Owner, scene.Target);
            entities.DeleteEntity(deleteTarget ? scene.Target : scene.Wolf);
        });
        await _pair.RunTicksSync(30);
        await _pair.Server.WaitAssertion(() =>
        {
            if (!deleteTarget)
            {
                Assert.That(entities.HasComponent<MedievalGuardedTargetComponent>(scene.Target), Is.False);
                return;
            }
            var pet = entities.GetComponent<MedievalCompanionComponent>(scene.Wolf);
            Assert.That(pet.GuardTarget, Is.Null);
            Assert.That(pet.Order, Is.EqualTo("follow"));
            Assert.That(entities.GetComponent<HTNComponent>(scene.Wolf).Blackboard
                .GetValue<EntityCoordinates>(NPCBlackboard.FollowTarget).EntityId, Is.EqualTo(scene.Owner));
        });
    }

    private void Guard(EntityUid owner, EntityUid target)
    {
        var entities = _pair.Server.EntMan;
        var actions = entities.System<SharedActionsSystem>();
        var action = actions.GetActions(owner).Single(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "ActionBookCompanionGuard");
        Assert.That(entities.HasComponent<InstantActionComponent>(action), Is.False);
        var targeting = entities.GetComponent<EntityTargetActionComponent>(action);
        Assert.That(targeting.Event, Is.TypeOf<BookCompanionGuardEvent>());
        Assert.That(actions.ValidateEntityTarget(owner, target, (action.Owner, targeting)), Is.True);
        var request = new BookCompanionGuardEvent { Target = target };
        actions.PerformAction(owner, action, request);
        Assert.That(request.Handled, Is.True);
    }

    private void Hit(EntityUid target, EntityUid attacker) => _pair.Server.System<DamageableSystem>().TryChangeDamage(target,
        new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, ignoreResistances: true, origin: attacker);

    private void AssertNear(EntityUid wolf, EntityUid target)
    {
        var entities = _pair.Server.EntMan;
        Assert.That(entities.GetComponent<TransformComponent>(wolf).Coordinates.TryDistance(entities,
            entities.GetComponent<TransformComponent>(target).Coordinates, out var distance), Is.True);
        Assert.That(distance, Is.LessThan(2.5f), Describe(wolf));
    }

    // Pathfinding schedules asynchronous jobs. Give those jobs time to complete between simulation batches.
    private async Task Advance(int ticks)
    {
        for (var elapsed = 0; elapsed < ticks; elapsed += 15)
        {
            await _pair.RunTicksSync(15);
            await Task.Delay(20);
        }
    }

    private string Describe(EntityUid wolf)
    {
        var entities = _pair.Server.EntMan;
        var htn = entities.GetComponent<HTNComponent>(wolf);
        var steering = entities.TryGetComponent<NPCSteeringComponent>(wolf, out var moving)
            ? $"{moving.Coordinates} ({moving.Status}, failed paths {moving.FailedPathCount})"
            : "none";
        return $"Wolf: {entities.GetComponent<TransformComponent>(wolf).Coordinates}; follow: " +
               $"{htn.Blackboard.GetValue<EntityCoordinates>(NPCBlackboard.FollowTarget)}; steering: {steering}; " +
               $"plan: {htn.Plan?.CurrentOperator.GetType().Name}; planning: {htn.PlanningJob?.Status}; enabled: {htn.Enabled}";
    }

    private async Task<(EntityUid Owner, EntityUid Wolf, EntityUid Target)> CreateScene(string targetPrototype)
    {
        var map = await _pair.CreateTestMap();
        var entities = _pair.Server.EntMan;
        (EntityUid, EntityUid, EntityUid) result = default;
        await _pair.Server.WaitAssertion(() =>
        {
            var tile = _pair.Server.Resolve<ITileDefinitionManager>()["Plating"].TileId;
            var maps = entities.System<SharedMapSystem>();
            // Leave room to circle the chest without stepping off the grid into space.
            for (var x = -12; x <= 24; x++)
            for (var y = -12; y <= 12; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), new Tile(tile));
            entities.System<GravitySystem>().EnableGravity(map.Grid, entities.EnsureComponent<GravityComponent>(map.Grid));
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = Atmospherics.OxygenMolesStandard;
            moles[(int) Gas.Nitrogen] = Atmospherics.NitrogenMolesStandard;
            entities.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false, new GasMixture(moles, Atmospherics.T20C));
            var owner = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var wolf = entities.SpawnEntity("MedievalMobWolf", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            var target = entities.SpawnEntity(targetPrototype, new EntityCoordinates(map.Grid, 5.5f, 0.5f));
            entities.RemoveComponent<ShieldOnStartupComponent>(owner);
            entities.RemoveComponent<ShieldOnStartupComponent>(target);
            Assert.That(entities.System<MedievalCompanionSystem>().Tame(wolf, owner), Is.True);
            result = (owner, wolf, target);
        });
        return result;
    }
}
