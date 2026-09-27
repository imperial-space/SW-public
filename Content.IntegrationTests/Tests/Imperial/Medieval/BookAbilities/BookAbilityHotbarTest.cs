using System.Linq;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.LocalLight;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Mind;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookAbilityHotbarTest
{
    [Test]
    public async Task LearnedAbilitiesCanBePinnedWithoutReplacingNativeActions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        await server.WaitAssertion(() =>
        {
            var user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            // Keep the UI fixture independent of the existing local-light teardown issue.
            server.EntMan.RemoveComponent<LocalLightComponent>(user);
            var knowledge = server.System<MedievalKnowledgeSystem>();
            Assert.That(knowledge.GrantKnowledge(user, "BookDiceCheat"), Is.True);
            Assert.That(knowledge.GrantKnowledge(user, "BookVentriloquism"), Is.True);
            var minds = server.System<SharedMindSystem>();
            minds.TransferTo(minds.CreateMind(server.PlayerMan.Sessions.Single().UserId), user);
        });
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            var controller = ui.GetUIController<ActionUIController>();
            var actions = client.System<ActionsSystem>();
            var previousContainer = ui.GetActiveUIWidgetOrNull<ActionsBar>()?.ActionsContainer;
            var container = new ActionButtonContainer();
            controller.RegisterActionContainer(container);
            try
            {
                actions.LinkAllActions();
                var owned = actions.GetClientActions().ToList();
                var native = owned.Where(action => action.Comp.AutoPopulate &&
                    !client.EntMan.HasComponent<BookAbilityActionComponent>(action)).Select(action => action.Owner).ToArray();
                var learned = owned.Where(action => client.EntMan.HasComponent<BookAbilityActionComponent>(action)).ToArray();
                Assert.That(native, Is.Not.Empty);
                Assert.That(learned, Has.Length.EqualTo(2));
                Assert.That(learned.All(action => !controller.IsActionOnHotbar(action)), Is.True,
                    "Learning abilities must not automatically fill the ordinary hotbar.");

                var ability = learned[0].Owner;
                Assert.That(controller.AddActionToHotbar(ability), Is.True);
                Assert.That(controller.AddActionToHotbar(ability), Is.True);
                Assert.That(container.GetButtons().Count(button => button.Action?.Owner == ability), Is.EqualTo(1),
                    "Repeated pinning must not duplicate an ability.");
                Assert.That(native.All(controller.IsActionOnHotbar), Is.True,
                    "Pinning a learned action must preserve combat, speech and the menu button.");
                Assert.That(controller.IsActionOnHotbar(learned[1]), Is.False);

                var icon = controller.CreateMenuActionButton(ability);
                Assert.That(icon.Action?.Owner, Is.EqualTo(ability));
                Assert.That(icon.Locked, Is.True, "Dropping another action onto a catalogue entry must not replace it.");
                Assert.DoesNotThrow(() => controller.ReleaseMenuActionButton(icon));
                icon.Dispose();

                var unowned = client.EntMan.SpawnEntity("ActionBookDiceCheat", MapCoordinates.Nullspace);
                Assert.That(controller.AddActionToHotbar(unowned), Is.False);
                client.EntMan.DeleteEntity(unowned);
            }
            finally
            {
                controller.RemoveActionContainer();
                container.Dispose();
                if (previousContainer != null)
                    controller.RegisterActionContainer(previousContainer);
            }
        });
        await pair.CleanReturnAsync();
    }
}
