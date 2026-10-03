using System.Numerics;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Medieval.BookAbilities;

public sealed class BookAbilityChoiceWindow : DefaultWindow
{
    private bool _answered;

    public BookAbilityChoiceWindow(BookAbilityChoicesEvent request, Action<string> reply)
    {
        Title = request.Title;
        MinSize = new Vector2(320, 160);
        SetSize = new Vector2(420, 440);
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        Contents.AddChild(scroll);
        var entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(entries);
        foreach (var option in request.Options)
        {
            var button = new Button
            {
                Text = option.Label,
                ToolTip = option.Label,
                ClipText = true,
                HorizontalExpand = true,
            };
            button.OnPressed += _ =>
            {
                if (_answered)
                    return;
                _answered = true;
                reply(option.Id);
                Close();
            };
            entries.AddChild(button);
        }

        var cancel = new Button { Text = Loc.GetString("book-abilities-choice-cancel"), HorizontalExpand = true };
        cancel.OnPressed += _ => Close();
        entries.AddChild(cancel);
        OnClose += () =>
        {
            if (!_answered)
            {
                _answered = true;
                reply(string.Empty);
            }
        };
    }
}
