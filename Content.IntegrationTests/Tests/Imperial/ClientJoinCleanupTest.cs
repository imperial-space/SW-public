using System.IO;
using Content.Client.ContextMenu.UI;
using Content.Client.Imperial.LocalLight;
using Content.Shared.Imperial.LocalLight;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class ClientJoinCleanupTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task OpenContextSubmenuClosesBeforeItsContentsAreDisposed(bool disposeElement)
    {
        using var client = CreateClient();
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            client.EntMan.Startup();
            var controller = new ContextMenuUIController();
            IoCManager.InjectDependencies(controller);
            controller.PostInject();
            controller.Setup();
            var root = controller.RootMenu;
            var element = new ContextMenuElement("entity");
            controller.AddElement(root, element);
            var submenu = new ContextMenuPopup(controller, element);
            var hidden = 0;
            var bodyWasAlive = false;
            submenu.OnPopupHide += () =>
            {
                bodyWasAlive = !submenu.MenuBody.Disposed;
                hidden++;
                submenu.MenuBody.RemoveAllChildren();
            };
            root.Open();
            controller.OpenSubMenu(element);
            Assert.That(submenu.Visible, Is.True);
            if (disposeElement)
                element.Dispose();
            else
                submenu.Dispose();
            Assert.That(hidden, Is.EqualTo(1));
            Assert.That(bodyWasAlive, Is.True);
            Assert.That(submenu.Visible, Is.False);
            Assert.That(submenu.Disposed, Is.True);
            Assert.That(submenu.Parent, Is.Null);
            Assert.DoesNotThrow(() => submenu.Dispose());
            Assert.That(controller.Menus.Peek(), Is.SameAs(root));
            element.Dispose();
            controller.Shutdown();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LocalLightCleansUpForPreviewDeletionAndComponentRemoval(bool removeComponent)
    {
        using var client = CreateClient();
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            client.EntMan.Startup();
            var entities = client.EntMan;
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            prototypes.LoadString("""
                - type: entity
                  id: JoinCleanupPreview
                  components:
                  - type: PointLight
                    netsync: false
                """);
            prototypes.ResolveResults();
            for (var i = 0; i < 3; i++)
            {
                var preview = entities.SpawnEntity(removeComponent ? null : "JoinCleanupPreview", MapCoordinates.Nullspace);
                entities.EnsureComponent<LocalLightComponent>(preview);
                var light = entities.GetComponent<PointLightComponent>(preview);
                Assert.That(light.NetSyncEnabled, Is.False);
                Assert.That(light.Enabled, Is.False);
                if (removeComponent)
                {
                    entities.RemoveComponent<LocalLightComponent>(preview);
                    Assert.That(entities.HasComponent<PointLightComponent>(preview), Is.False);
                }
                entities.DeleteEntity(preview);
                Assert.That(entities.EntityExists(preview), Is.False);
            }
        });
    }

    private static RobustIntegrationTest.ClientIntegrationInstance CreateClient()
        => new(new()
        {
            ContentStart = false,
            LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false,
                LoadConfigAndUserData = false,
                PrototypeDirectory = new("/JoinCleanupNoWorld/"),
                MountOptions = new()
                {
                    DirMounts =
                    {
                        Path.GetFullPath("../../Resources", TestContext.CurrentContext.TestDirectory),
                        Path.GetFullPath("../../RobustToolbox/Resources", TestContext.CurrentContext.TestDirectory)
                    }
                }
            },
            BeforeRegisterComponents = () => IoCManager.Resolve<IComponentFactory>().RegisterClass<LocalLightComponent>(),
            BeforeStart = () => IoCManager.Resolve<IEntitySystemManager>().LoadExtraSystemType<LocalLightSystem>()
        });
}
