using System.Numerics;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Medieval.BookAbilities;

/// <summary>A single entry point for learned actions and their passive counterparts.</summary>
public sealed class BookAbilitiesWindow : DefaultWindow
{
    public readonly BoxContainer Entries;

    public BookAbilitiesWindow()
    {
        Title = Loc.GetString("book-abilities-menu-title");
        MinSize = new Vector2(400, 300);
        SetSize = new Vector2(520, 620);
        var layout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };
        Contents.AddChild(layout);
        layout.AddChild(new RichTextLabel { Text = Loc.GetString("book-abilities-menu-hint") });
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        layout.AddChild(scroll);
        Entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        scroll.AddChild(Entries);
    }

    public void AddSection(string title)
    {
        Entries.AddChild(new Label { Text = title, Margin = new Thickness(0, 8, 0, 0) });
    }

    public Button AddAbility(string name, string description, Action? activate, Texture? icon = null)
    {
        var row = new BoxContainer { SeparationOverride = 8 };
        if (icon != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = icon,
                MinSize = new Vector2(64, 64),
                MaxSize = new Vector2(64, 64),
                VerticalAlignment = VAlignment.Top,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
            });
        }
        var details = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
        };
        var button = new Button
        {
            Text = name,
            ToolTip = name,
            ClipText = true,
            HorizontalExpand = true,
            Disabled = activate == null,
        };
        if (activate != null)
            button.OnPressed += _ => activate();
        details.AddChild(button);
        details.AddChild(new RichTextLabel { Text = description, Margin = new Thickness(6, 0, 6, 4) });
        row.AddChild(details);
        Entries.AddChild(row);
        return button;
    }

    public (Button Activate, Button Pin) AddActiveAbility(string name, string description, ActionButton icon,
        Action activate, Action pin)
    {
        var row = new BoxContainer { SeparationOverride = 8 };
        icon.MinSize = new Vector2(64, 64);
        icon.MaxSize = new Vector2(64, 64);
        icon.VerticalAlignment = VAlignment.Top;
        row.AddChild(icon);

        var details = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
        };
        var controls = new BoxContainer { SeparationOverride = 4 };
        var activateButton = new Button { Text = name, ClipText = true, HorizontalExpand = true, ToolTip = name };
        activateButton.OnPressed += _ => activate();
        var pinButton = new Button
        {
            Text = Loc.GetString("book-abilities-menu-pin"),
            ToolTip = Loc.GetString("book-abilities-menu-pin-hint"),
        };
        pinButton.OnPressed += _ => pin();
        controls.AddChild(activateButton);
        controls.AddChild(pinButton);
        details.AddChild(controls);
        details.AddChild(new RichTextLabel { Text = description, Margin = new Thickness(0, 0, 0, 4) });
        row.AddChild(details);
        Entries.AddChild(row);
        return (activateButton, pinButton);
    }
}
