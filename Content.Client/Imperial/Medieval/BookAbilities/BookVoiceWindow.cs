using System.Numerics;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Medieval.BookAbilities;

public sealed class BookVoiceWindow : DefaultWindow
{
    private readonly LineEdit _input;
    private bool _answered;

    public BookVoiceWindow(BookVoicePromptEvent request, Action<string> reply)
    {
        Title = Loc.GetString("book-voice-title");
        MinSize = new Vector2(360, 150);
        SetSize = new Vector2(520, 160);
        var entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        Contents.AddChild(entries);
        entries.AddChild(new Label
        {
            Text = Loc.GetString("book-voice-target", ("target", request.TargetName)),
            ClipText = true,
            ToolTip = request.TargetName,
        });
        _input = new LineEdit
        {
            PlaceHolder = Loc.GetString("book-voice-placeholder"),
            HorizontalExpand = true,
            IsValid = text => text.Length <= request.MaxLength,
        };
        entries.AddChild(_input);
        var buttons = new BoxContainer { SeparationOverride = 8, HorizontalExpand = true };
        entries.AddChild(buttons);
        var submit = new Button
        {
            Text = Loc.GetString("book-voice-send"),
            HorizontalExpand = true,
            Disabled = true,
        };
        var cancel = new Button
        {
            Text = Loc.GetString("book-abilities-choice-cancel"),
            HorizontalExpand = true,
        };
        buttons.AddChild(submit);
        buttons.AddChild(cancel);
        _input.OnTextChanged += args => submit.Disabled = string.IsNullOrWhiteSpace(args.Text);
        _input.OnTextEntered += _ => Submit();
        submit.OnPressed += _ => Submit();
        cancel.OnPressed += _ => Close();
        OnClose += () =>
        {
            if (_answered) return;
            _answered = true;
            reply(string.Empty);
        };

        void Submit()
        {
            if (_answered || string.IsNullOrWhiteSpace(_input.Text)) return;
            _answered = true;
            reply(_input.Text.Trim());
            Close();
        }
    }

    public void FocusInput() => _input.GrabKeyboardFocus();
}
