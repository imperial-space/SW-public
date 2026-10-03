using Content.Shared.Imperial.Medieval.BookAbilities;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Medieval.BookAbilities;

public sealed class BookVoiceSystem : EntitySystem
{
    private BookVoiceWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BookVoicePromptEvent>(OnPrompt);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ => CloseWindow());
    }

    public override void Shutdown()
    {
        CloseWindow();
        base.Shutdown();
    }

    private void OnPrompt(BookVoicePromptEvent args)
    {
        CloseWindow();
        var window = new BookVoiceWindow(args,
            text => RaiseNetworkEvent(new BookVoiceSubmittedEvent(args.RequestId, text)));
        _window = window;
        window.OnClose += () =>
        {
            if (_window == window)
                _window = null;
        };
        window.OpenCentered();
        window.FocusInput();
    }

    private void CloseWindow()
    {
        _window?.Close();
        _window = null;
    }
}
