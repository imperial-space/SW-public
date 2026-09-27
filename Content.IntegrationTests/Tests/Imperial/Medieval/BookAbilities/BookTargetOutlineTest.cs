using System.Linq;
using Content.Client.ContextMenu.UI;
using Content.Client.Actions;
using Content.Client.Imperial.Medieval.BookAbilities;
using Content.Client.UserInterface.Systems.Actions;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.LocalLight;
using Content.Shared.Mind;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookTargetOutlineTest
{
    [Test]
    public async Task BookTargetingHighlightsOnlyTheHoveredEntityAndClearsItOnUiHover()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        EntityUid first = default;
        EntityUid second = default;
        await server.WaitAssertion(() =>
        {
            var user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            // Keep this client UI regression independent of the known local-light teardown defect.
            server.EntMan.RemoveComponent<LocalLightComponent>(user);
            Assert.That(server.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookVentriloquism"), Is.True);
            var minds = server.System<SharedMindSystem>();
            minds.TransferTo(minds.CreateMind(server.PlayerMan.Sessions.Single().UserId), user);
            first = server.EntMan.SpawnEntity("d6Dice", map.GridCoords);
            second = server.EntMan.SpawnEntity("d6Dice", map.GridCoords);
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            var outline = client.System<BookAbilityTargetOutlineSystem>();
            var controller = ui.GetUIController<ActionUIController>();
            var action = client.System<ActionsSystem>().GetClientActions().Single(candidate =>
                client.EntMan.GetComponent<MetaDataComponent>(candidate).EntityPrototype?.ID == "ActionBookVentriloquism");
            var firstSprite = client.EntMan.GetComponent<SpriteComponent>(pair.ToClientUid(first));
            var secondSprite = client.EntMan.GetComponent<SpriteComponent>(pair.ToClientUid(second));
            var firstEntry = new EntityMenuElement(pair.ToClientUid(first));
            var secondEntry = new EntityMenuElement(pair.ToClientUid(second));
            var ordinaryControl = new Button();
            try
            {
                controller.ActivateAction(action.Owner);
                Assert.That(controller.SelectingTargetFor, Is.EqualTo(action.Owner));
                ui.SetHovered(firstEntry);
                outline.FrameUpdate(0);
                Assert.That(firstSprite.PostShader, Is.Not.Null);
                Assert.That(secondSprite.PostShader, Is.Null, "A neighbour on the same tile must not be highlighted.");

                ui.SetHovered(secondEntry);
                outline.FrameUpdate(0);
                Assert.That(firstSprite.PostShader, Is.Null);
                Assert.That(secondSprite.PostShader, Is.Not.Null);

                ui.SetHovered(ordinaryControl);
                outline.FrameUpdate(0);
                Assert.That(firstSprite.PostShader, Is.Null);
                Assert.That(secondSprite.PostShader, Is.Null, "Hovering ordinary UI must clear the previous target.");
                Assert.That(controller.SelectingTargetFor, Is.EqualTo(action.Owner),
                    "Moving onto UI does not cancel the ability itself.");

                ui.SetHovered(firstEntry);
                outline.FrameUpdate(0);
                Assert.That(firstSprite.PostShader, Is.Not.Null);
                controller.ActivateAction(action.Owner);
                Assert.That(controller.SelectingTargetFor, Is.Null, "Activating the same ability cancels targeting.");
                outline.FrameUpdate(0);
                Assert.That(firstSprite.PostShader, Is.Null, "Leaving book targeting removes its own shader.");
                Assert.That(secondSprite.PostShader, Is.Null);
            }
            finally
            {
                if (controller.SelectingTargetFor == action.Owner)
                    controller.ActivateAction(action.Owner);
                ui.SetHovered(null);
                outline.FrameUpdate(0);
                firstEntry.Dispose();
                secondEntry.Dispose();
                ordinaryControl.Dispose();
            }
        });
        await pair.CleanReturnAsync();
    }
}
