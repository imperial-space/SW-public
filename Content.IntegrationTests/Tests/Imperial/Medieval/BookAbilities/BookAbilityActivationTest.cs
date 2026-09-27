using System.Linq;
using Content.Server.Imperial.Medieval.BookAbilities;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.LocalLight;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookAbilityActivationTest
{
    [Test]
    public async Task DiceAbilityPrefersRightHandEvenWhenLeftHandIsActive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var leftDie = entities.SpawnEntity("d6Dice", map.GridCoords);
            var rightDie = entities.SpawnEntity("d6Dice", map.GridCoords);
            var hands = entities.GetComponent<HandsComponent>(user);
            var handSystem = entities.System<SharedHandsSystem>();
            var left = hands.Hands.Single(hand => hand.Value.Location == HandLocation.Left).Key;
            var right = hands.Hands.Single(hand => hand.Value.Location == HandLocation.Right).Key;
            Assert.That(handSystem.TryPickup(user, leftDie, left), Is.True);
            Assert.That(handSystem.TryPickup(user, rightDie, right), Is.True);
            handSystem.TrySetActiveHand(user, left);
            Assert.That(handSystem.GetActiveItem(user), Is.EqualTo(leftDie));
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookDiceCheat");
            var abilities = entities.System<MedievalBookAbilitySystem>();

            Assert.That(abilities.FindHeldDie(user), Is.EqualTo(rightDie));
            Assert.That(abilities.TryPrepareHeldDie(user, 6), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<BookLoadedDiceComponent>(leftDie), Is.False);
                Assert.That(entities.GetComponent<BookLoadedDiceComponent>(rightDie).Side, Is.EqualTo(6));
                Assert.That(entities.GetComponent<BookLoadedDiceComponent>(rightDie).User, Is.EqualTo(user));
            });

            Assert.That(handSystem.TryDrop(user, rightDie), Is.True);
            Assert.That(abilities.FindHeldDie(user), Is.EqualTo(leftDie));
            Assert.That(abilities.TryPrepareHeldDie(user, 2), Is.True);
            Assert.That(entities.GetComponent<BookLoadedDiceComponent>(leftDie).Side, Is.EqualTo(2));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DiceAbilityRequiresKnowledgeHeldDieAndValidFace()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var die = entities.SpawnEntity("d6Dice", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var abilities = entities.System<MedievalBookAbilitySystem>();
            Assert.That(hands.TryPickupAnyHand(user, die), Is.True);
            Assert.That(abilities.TryPrepareHeldDie(user, 6), Is.False, "Holding a die does not teach the ability.");
            Assert.That(entities.HasComponent<BookLoadedDiceComponent>(die), Is.False);

            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookDiceCheat");
            Assert.That(hands.TryDrop(user, die), Is.True);
            Assert.That(abilities.FindHeldDie(user), Is.Null);
            Assert.That(abilities.TryPrepareHeldDie(user, 6), Is.False, "A nearby die on the floor is not held.");
            var pen = entities.SpawnEntity("MedievalPen", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, pen), Is.True);
            Assert.That(abilities.TryPrepareHeldDie(user, 6), Is.False, "Other held items cannot become loaded dice.");
            Assert.That(hands.TryPickupAnyHand(user, die), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(abilities.TryPrepareHeldDie(user, 0), Is.False);
                Assert.That(abilities.TryPrepareHeldDie(user, 7), Is.False);
                Assert.That(entities.HasComponent<BookLoadedDiceComponent>(die), Is.False);
                Assert.That(entities.HasComponent<BookLoadedDiceComponent>(pen), Is.False);
            });
            Assert.That(abilities.TryPrepareHeldDie(user, 1), Is.True);
            Assert.That(entities.GetComponent<BookLoadedDiceComponent>(die).Side, Is.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PassiveKnowledgeGrantsOneMenuWhichFollowsTheMind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var first = entities.SpawnEntity(null, map.GridCoords);
            var second = entities.SpawnEntity(null, map.GridCoords);
            entities.EnsureComponent<MindContainerComponent>(first);
            entities.EnsureComponent<MindContainerComponent>(second);
            var minds = entities.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, first);
            var knowledge = entities.System<MedievalKnowledgeSystem>();
            var actions = entities.System<SharedActionsSystem>();
            Assert.That(knowledge.GrantKnowledge(first, "BookLipReading"), Is.True);
            Assert.That(knowledge.GrantKnowledge(first, "BookDisarmingRiposte"), Is.True);
            Assert.That(knowledge.GrantKnowledge(first, "BookLipReading"), Is.False);
            var owned = actions.GetActions(first).ToList();
            Assert.That(owned, Has.Count.EqualTo(1), "A passive-only character still has exactly one menu.");
            var menu = owned.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MetaDataComponent>(menu).EntityPrototype?.ID, Is.EqualTo("ActionBookAbilitiesMenu"));
                Assert.That(menu.Comp.AutoPopulate, Is.True);
                Assert.That(menu.Comp.ClientExclusive, Is.True);
                Assert.That(menu.Comp.CheckCanInteract, Is.False, "The menu must remain accessible while restrained.");
            });

            minds.TransferTo(mind, second);
            Assert.That(actions.GetActions(first), Is.Empty);
            Assert.That(actions.GetActions(second).Select(action => action.Owner), Is.EquivalentTo(new[] { menu.Owner }));
            Assert.That(knowledge.HasKnowledge(second, "BookLipReading"), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArchivistMenuReplicatesToClientAndFollowsTheMind()
    {
        TestContext.Progress.WriteLine("Starting the connected client/server pair on Empty.");
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        TestContext.Progress.WriteLine("Client connected; replicating the archivist's initial knowledge.");
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        var knowledge = server.System<MedievalKnowledgeSystem>();
        var minds = server.System<SharedMindSystem>();
        var actions = server.System<SharedActionsSystem>();
        EntityUid first = default;
        EntityUid second = default;
        EntityUid mind = default;
        EntityUid menu = default;
        EntityUid[] firstNativeActions = [];
        EntityUid[] secondNativeActions = [];
        await server.WaitAssertion(() =>
        {
            first = entities.SpawnEntity("MobHuman", map.GridCoords);
            // Local lighting has an unrelated client teardown bug; this fixture tests mind actions.
            entities.RemoveComponent<LocalLightComponent>(first);
            firstNativeActions = actions.GetActions(first).Select(action => action.Owner).ToArray();
            Assert.That(firstNativeActions, Is.Not.Empty);
            Assert.That(firstNativeActions.Select(action => entities.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID),
                Does.Contain("ActionCombatModeToggle"));
            Assert.That(firstNativeActions.Select(action => entities.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID),
                Does.Contain("ActionScream"));
            // Profession knowledge can arrive before the player's mind is attached.
            Assert.That(knowledge.GrantKnowledge(first, "BookDecipherer"), Is.True);
            mind = minds.CreateMind(server.PlayerMan.Sessions.Single().UserId);
            minds.TransferTo(mind, first);
            Assert.That(firstNativeActions.All(action => actions.GetActions(first).Any(owned => owned.Owner == action)),
                Is.True, "Attaching the archivist's mind must preserve all native body actions.");
            menu = actions.GetActions(first).Single(action =>
                entities.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == "ActionBookAbilitiesMenu");
        });
        await pair.RunTicksSync(10);

        void AssertClientKnowledge(EntityUid body, bool hasDice, EntityUid[] nativeActions)
        {
            var clientBody = pair.ToClientUid(body);
            Assert.That(client.PlayerMan.LocalEntity, Is.EqualTo(clientBody));
            var learned = client.EntMan.GetComponent<LearnedKnowledgeComponent>(clientBody);
            Assert.That(learned.Knowledge, Does.Contain("BookDecipherer"));
            var owned = client.System<SharedActionsSystem>().GetActions(clientBody)
                .Where(action => client.EntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID.StartsWith("ActionBook") == true)
                .ToList();
            Assert.That(owned, Has.Count.EqualTo(hasDice ? 2 : 1));
            var clientMenu = owned.Single(action => action.Owner == pair.ToClientUid(menu));
            Assert.That(clientMenu.Comp.AttachedEntity, Is.EqualTo(clientBody));
            Assert.That(learned.Knowledge.Contains("BookDiceCheat"), Is.EqualTo(hasDice));
            var allActions = client.System<SharedActionsSystem>().GetActions(clientBody).ToList();
            foreach (var native in nativeActions)
            {
                var clientAction = pair.ToClientUid(native);
                Assert.That(allActions.Any(action => action.Owner == clientAction), Is.True,
                    "Learning or transferring book abilities must not remove native actions on the client.");
                Assert.That(client.EntMan.HasComponent<BookAbilityActionComponent>(clientAction), Is.False);
            }
        }

        await client.WaitAssertion(() => AssertClientKnowledge(first, false, firstNativeActions));
        await server.WaitAssertion(() => Assert.That(knowledge.GrantKnowledge(first, "BookDiceCheat"), Is.True));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => AssertClientKnowledge(first, true, firstNativeActions));

        await server.WaitPost(() =>
        {
            second = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.RemoveComponent<LocalLightComponent>(second);
            secondNativeActions = actions.GetActions(second).Select(action => action.Owner).ToArray();
            minds.TransferTo(mind, second);
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() =>
        {
            AssertClientKnowledge(second, true, secondNativeActions);
            var oldBody = pair.ToClientUid(first);
            Assert.That(client.EntMan.GetComponent<LearnedKnowledgeComponent>(oldBody).Knowledge, Is.Empty);
            Assert.That(client.System<SharedActionsSystem>().GetActions(oldBody).Any(action =>
                client.EntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID.StartsWith("ActionBook") == true), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyTheMenuAutomaticallyPopulatesTheActionBar()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var knowledge = entities.System<MedievalKnowledgeSystem>();
            var actions = entities.System<SharedActionsSystem>();
            foreach (var entry in prototypes.EnumeratePrototypes<MedievalKnowledgePrototype>())
                knowledge.GrantKnowledge(user, entry.ID);

            var owned = actions.GetActions(user)
                .Where(action => entities.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID.StartsWith("ActionBook") == true)
                .ToList();
            Assert.That(owned.Count, Is.GreaterThan(1));
            var visible = owned.Where(action => action.Comp.AutoPopulate).ToList();
            Assert.That(visible, Has.Count.EqualTo(1));
            Assert.That(entities.GetComponent<MetaDataComponent>(visible.Single()).EntityPrototype?.ID, Is.EqualTo("ActionBookAbilitiesMenu"));
            foreach (var action in owned)
                Assert.That(entities.HasComponent<BookAbilityActionComponent>(action), Is.EqualTo(!action.Comp.AutoPopulate),
                    "Only individual book abilities belong in the book menu; its opening button belongs on the action bar.");

            foreach (var id in new[]
                     {
                         "ActionBookCompanionFollow", "ActionBookCompanionStay", "ActionBookCompanionGuard",
                         "ActionBookCompanionRetreat", "ActionBookCompanionAttack", "ActionBookCancelTaming",
                     })
            {
                var action = entities.SpawnEntity(id, map.GridCoords);
                Assert.That(entities.GetComponent<ActionComponent>(action).AutoPopulate, Is.False, id);
                Assert.That(entities.HasComponent<BookAbilityActionComponent>(action), Is.True, id);
            }
        });
        await pair.CleanReturnAsync();
    }
}
