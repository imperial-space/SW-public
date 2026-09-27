using Content.Client.ContextMenu.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookContextMenuTest
{
    [Test]
    public async Task RemovingAnEntityWithAnOpenVerbSubmenuDoesNotCrash()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var context = ui.GetUIController<ContextMenuUIController>();
            context.Setup();
            var element = new ContextMenuElement("A workstation that leaves view when packed");
            context.AddElement(context.RootMenu, element);
            var submenu = new ContextMenuPopup(context, element);
            submenu.MenuBody.AddChild(new Label { Text = "Inspect" });
            // EntityMenuUIController installs this callback for every entity's verb submenu.
            submenu.OnPopupHide += submenu.MenuBody.RemoveAllChildren;
            context.RootMenu.Open();
            context.OpenSubMenu(element);
            Assert.That(submenu.Visible, Is.True);
            Assert.That(context.Menus, Does.Contain(submenu));

            // EntityMenuUIController.RemoveEntity disposes the element when a target disappears.
            Assert.DoesNotThrow(element.Dispose);
            Assert.That(submenu.Disposed, Is.True);
            Assert.That(context.Menus, Does.Not.Contain(submenu));
            Assert.DoesNotThrow(context.Close);
            Assert.DoesNotThrow(context.Shutdown);
        });
        await pair.CleanReturnAsync();
    }
}
