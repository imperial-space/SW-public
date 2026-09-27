using System.Linq;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Blocking;
using Content.Shared.Dice;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Throwing;
using Content.Shared.Toggleable;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookIsolationTest
{
    [Test]
    public async Task BookActionsKeepIdentityAndCooldownWithoutRegrantingForeignActions()
    {
        await using var pair = await BookTestServer.Create();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid learnedAction = default;
        EntityUid menu = default;
        await pair.Server.WaitAssertion(() =>
        {
            var first = entities.SpawnEntity(null, map.GridCoords);
            var second = entities.SpawnEntity(null, map.GridCoords);
            entities.EnsureComponent<MindContainerComponent>(first);
            entities.EnsureComponent<MindContainerComponent>(second);
            var knowledge = entities.System<MedievalKnowledgeSystem>();
            var actions = entities.System<SharedActionsSystem>();
            var containers = entities.System<ActionContainerSystem>();
            var minds = entities.System<SharedMindSystem>();
            var timing = pair.Server.Resolve<IGameTiming>();

            // Profession knowledge can precede the first mind attachment.
            knowledge.GrantKnowledge(first, "BookEscapeBonds");
            var original = entities.GetComponent<LearnedKnowledgeComponent>(first).GrantedActions;
            learnedAction = original["ActionBookEscapeBonds"];
            menu = original["ActionBookAbilitiesMenu"];
            actions.SetCooldown(learnedAction, timing.CurTime, timing.CurTime + TimeSpan.FromMinutes(2));
            var cooldown = entities.GetComponent<ActionComponent>(learnedAction).Cooldown;
            EntityUid? ordinary = null;
            Assert.That(actions.AddAction(first, ref ordinary, "HideHairAction"), Is.True);

            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, first);
            var saved = entities.GetComponent<LearnedKnowledgeComponent>(mind);
            Assert.That(saved.GrantedActions["ActionBookEscapeBonds"], Is.EqualTo(learnedAction));
            Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Container, Is.EqualTo(first));
            Assert.That(entities.HasComponent<ActionsContainerComponent>(mind), Is.False);

            var foreign = containers.AddAction(mind, "HideHairAction")!.Value;
            var foreignComp = entities.GetComponent<ActionComponent>(foreign);
            foreignComp.StartDelay = true;
            foreignComp.UseDelay = TimeSpan.FromSeconds(10);
            actions.SetCooldown(foreign, timing.CurTime, timing.CurTime + TimeSpan.FromMinutes(3));
            var foreignCooldown = foreignComp.Cooldown;
            knowledge.GrantKnowledge(first, "BookLanguageElf");
            Assert.That(foreignComp.Cooldown, Is.EqualTo(foreignCooldown), "Learning must not re-grant unrelated mind actions.");

            minds.TransferTo(mind, null, createGhost: false);
            Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Container, Is.Null);
            Assert.That(entities.GetComponent<ActionComponent>(learnedAction).AttachedEntity, Is.Null);
            Assert.That(actions.GetActions(first).Select(action => action.Owner), Does.Contain(ordinary!.Value));
            minds.TransferTo(mind, second);
            Assert.Multiple(() =>
            {
                Assert.That(saved.GrantedActions["ActionBookEscapeBonds"], Is.EqualTo(learnedAction));
                Assert.That(saved.GrantedActions["ActionBookAbilitiesMenu"], Is.EqualTo(menu));
                Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Cooldown, Is.EqualTo(cooldown));
                Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Container, Is.EqualTo(second));
                Assert.That(entities.GetComponent<ActionComponent>(ordinary.Value).Container, Is.EqualTo(first));
                Assert.That(foreignComp.Container, Is.EqualTo(mind.Owner));
                Assert.That(knowledge.HasKnowledge(first, "BookEscapeBonds"), Is.False);
                Assert.That(knowledge.HasKnowledge(second, "BookEscapeBonds"), Is.True);
            });
            entities.DeleteEntity(mind);
        });
        await pair.RunTicksSync(1);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(learnedAction), Is.False, "Deleting the mind must clean up its parked or attached book actions.");
            Assert.That(entities.EntityExists(menu), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingTheBodyThenVisitingAGhostPreservesOnlyBookActions()
    {
        await using var pair = await BookTestServer.Create();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid mind = default;
        EntityUid learnedAction = default;
        EntityUid menu = default;
        EntityUid ordinary = default;
        ActionCooldown? cooldown = null;
        await pair.Server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity(null, map.GridCoords);
            // BookTestServer keeps the dummy ticker in the lobby; select an observer explicitly below.
            entities.EnsureComponent<MindContainerComponent>(body);
            var minds = entities.System<SharedMindSystem>();
            mind = minds.CreateMind(null);
            minds.TransferTo(mind, body);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(body, "BookEscapeBonds");
            var saved = entities.GetComponent<LearnedKnowledgeComponent>(mind);
            learnedAction = saved.GrantedActions["ActionBookEscapeBonds"];
            menu = saved.GrantedActions["ActionBookAbilitiesMenu"];
            var actions = entities.System<SharedActionsSystem>();
            var now = pair.Server.Resolve<IGameTiming>().CurTime;
            actions.SetCooldown(learnedAction, now, now + TimeSpan.FromMinutes(3));
            cooldown = entities.GetComponent<ActionComponent>(learnedAction).Cooldown;
            EntityUid? added = null;
            Assert.That(actions.AddAction(body, ref added, "HideHairAction"), Is.True);
            ordinary = added.Value;
            entities.DeleteEntity(body);
        });
        // Entity deletion must finish before the next body is assigned, not merely a same-tick transfer.
        await pair.RunTicksSync(1);
        await pair.Server.WaitAssertion(() =>
        {
            var actions = entities.System<SharedActionsSystem>();
            var minds = entities.System<SharedMindSystem>();
            var saved = entities.GetComponent<LearnedKnowledgeComponent>(mind);
            Assert.That(entities.EntityExists(ordinary), Is.False);
            Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Container, Is.Null);
            Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Cooldown, Is.EqualTo(cooldown));
            Assert.That(saved.GrantedActions.Values, Is.EquivalentTo(new[] { learnedAction, menu }));

            var ghost = entities.SpawnEntity("MobObserver", map.GridCoords);
            Assert.That(entities.HasComponent<GhostComponent>(ghost), Is.True);
            minds.TransferTo(mind, ghost);
            var ghostActions = actions.GetActions(ghost).Select(action => action.Owner).ToArray();
            Assert.That(ghostActions, Is.Not.Empty, "Exercise a real observer with its own action set.");
            Assert.That(ghostActions.Intersect(saved.GrantedActions.Values), Is.Empty);
            Assert.That(entities.GetComponent<ActionComponent>(learnedAction).AttachedEntity, Is.Null);
            Assert.That(entities.GetComponent<ActionComponent>(menu).AttachedEntity, Is.Null);

            var newBody = entities.SpawnEntity(null, map.GridCoords);
            EntityUid? newBodyAction = null;
            Assert.That(actions.AddAction(newBody, ref newBodyAction, "HideHairAction"), Is.True);
            minds.TransferTo(mind, newBody);
            Assert.Multiple(() =>
            {
                Assert.That(saved.GrantedActions.Values, Is.EquivalentTo(new[] { learnedAction, menu }));
                Assert.That(saved.GrantedActions.Values.Intersect(ghostActions), Is.Empty);
                Assert.That(saved.GrantedActions.Values, Does.Not.Contain(newBodyAction!.Value));
                Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Cooldown, Is.EqualTo(cooldown));
                Assert.That(entities.GetComponent<ActionComponent>(learnedAction).Container, Is.EqualTo(newBody));
                Assert.That(entities.GetComponent<ActionComponent>(menu).Container, Is.EqualTo(newBody));
                Assert.That(actions.GetActions(newBody).Select(action => action.Owner),
                    Is.EquivalentTo(new[] { learnedAction, menu, newBodyAction.Value }));
                Assert.That(entities.System<MedievalKnowledgeSystem>().HasKnowledge(newBody, "BookEscapeBonds"), Is.True);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MobileAndBrokenBlockingDoNotChangeOrdinaryShieldBehavior()
    {
        await using var pair = await BookTestServer.Create();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var shield = entities.SpawnEntity("MedievalIronShield", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, shield), Is.True);
            var blocking = entities.System<BlockingSystem>();
            var shieldComp = entities.GetComponent<BlockingComponent>(shield);
            Assert.That(blocking.StartBlocking(shield, shieldComp, user), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(user).Anchored, Is.True);
            blocking.StopBlocking(shield, shieldComp, user);

            var neighbour = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(blocking.StartBlocking(shield, shieldComp, user), Is.False,
                "Ordinary blocking must reject a tile occupied by another mob.");
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookMobileBlock");
            Assert.That(blocking.StartBlocking(shield, shieldComp, user), Is.False,
                "Mobile blocking must retain the same occupied-tile restriction.");
            Assert.That(shieldComp.IsBlocking, Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(user).Anchored, Is.False);
            entities.DeleteEntity(neighbour);
            Assert.That(blocking.StartBlocking(shield, shieldComp, user), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(user).Anchored, Is.False);
            blocking.StopBlocking(shield, shieldComp, user);
            entities.EnsureComponent<ShieldSkillComponent>(user).MinBlockFraction = 0.9f;
            var broken = entities.EnsureComponent<BookBrokenGuardComponent>(shield);
            broken.Until = pair.Server.Resolve<IGameTiming>().CurTime + TimeSpan.FromSeconds(5);
            entities.EventBus.RaiseLocalEvent(shield, new ToggleActionEvent { Performer = user });
            Assert.That(shieldComp.IsBlocking, Is.False);
            var fraction = new GetShieldBlockFractionEvent(shield, user, 0.5f);
            entities.EventBus.RaiseLocalEvent(user, ref fraction);
            Assert.That(fraction.BlockFraction, Is.Zero, "Broken guard must run after ordinary shield skill bonuses.");

            broken.Until = TimeSpan.Zero;
            fraction = new GetShieldBlockFractionEvent(shield, user, 0.5f);
            entities.EventBus.RaiseLocalEvent(user, ref fraction);
            Assert.That(fraction.BlockFraction, Is.EqualTo(0.9f));
            entities.EventBus.RaiseLocalEvent(shield, new ToggleActionEvent { Performer = user });
            Assert.That(shieldComp.IsBlocking, Is.True);
            blocking.StopBlocking(shield, shieldComp, user);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LoadedDiceApplyOnceForTheirUserOnBothUseAndLanding()
    {
        await using var pair = await BookTestServer.Create();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var other = entities.SpawnEntity("MobHuman", map.GridCoords);
            var die = entities.SpawnEntity("d6Dice", map.GridCoords);
            var loaded = entities.EnsureComponent<BookLoadedDiceComponent>(die);
            loaded.User = user;
            loaded.Side = 6;
            entities.EventBus.RaiseLocalEvent(die, new UseInHandEvent(other));
            Assert.That(entities.HasComponent<BookLoadedDiceComponent>(die), Is.True);
            Assert.That(entities.GetComponent<DiceComponent>(die).CurrentValue, Is.InRange(1, 6));
            var land = new LandEvent(user, true);
            entities.EventBus.RaiseLocalEvent(die, ref land);
            Assert.That(entities.GetComponent<DiceComponent>(die).CurrentValue, Is.EqualTo(6));
            Assert.That(entities.HasComponent<BookLoadedDiceComponent>(die), Is.False);
            loaded = entities.EnsureComponent<BookLoadedDiceComponent>(die);
            loaded.User = user;
            loaded.Side = 1;
            entities.EventBus.RaiseLocalEvent(die, new UseInHandEvent(user));
            Assert.That(entities.GetComponent<DiceComponent>(die).CurrentValue, Is.EqualTo(1));
            Assert.That(entities.HasComponent<BookLoadedDiceComponent>(die), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
