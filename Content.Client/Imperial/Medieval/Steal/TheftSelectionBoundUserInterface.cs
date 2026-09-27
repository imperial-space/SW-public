using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.RandomSteal.Events;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Steal;

[UsedImplicitly]
public sealed class TheftSelectionBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BoxContainer? _items;

    protected override void Open()
    {
        base.Open();
        var window = this.CreateWindow<FancyWindow>();
        window.Title = Loc.GetString("skills-theft-choose");
        window.SetSize = new Vector2(340, 360);
        _items = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        scroll.AddChild(_items);
        window.ContentsContainer.AddChild(scroll);
        SendMessage(new RequestTheftChoicesMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not TheftChoicesMessage choices || _items is not { } items)
            return;

        items.RemoveAllChildren();
        if (choices.Items.Count == 0)
            items.AddChild(new Label { Text = Loc.GetString("skills-theft-empty") });

        foreach (var item in choices.Items)
        {
            var button = new Button { HorizontalExpand = true, ToolTip = item.Name };
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            if (item.Prototype is { } prototype)
            {
                var icon = new EntityPrototypeView { SetSize = new Vector2(32, 32), MouseFilter = Control.MouseFilterMode.Ignore };
                icon.SetPrototype(prototype);
                row.AddChild(icon);
            }
            row.AddChild(new Label { Text = item.Name, ClipText = true, HorizontalExpand = true });
            button.AddChild(row);
            button.OnPressed += _ =>
            {
                foreach (var child in items.Children)
                    if (child is Button choice)
                        choice.Disabled = true;
                SendMessage(new SelectTheftItemMessage(item.Entity));
            };
            items.AddChild(button);
        }
    }
}
