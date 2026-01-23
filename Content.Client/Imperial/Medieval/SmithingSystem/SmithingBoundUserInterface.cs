using Content.Shared.Imperial.Medieval.SmithingSystem;
using Content.Shared.Imperial.Medieval.SmithingSystem.Bui;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.SmithingSystem;

public sealed class SmithingBoundUserInterface : BoundUserInterface
{
    private SmithingWindow? _window;

    public SmithingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SmithingWindow>();
        _window.OpenCentered();

        _window.HitMissed += OnHitMissed;
        _window.TargetHit += OnTargetHit;
        _window.StartGamePressed += OnStartGamePressed;
        _window.GameEnded += OnGameEnded;
        _window.TargetExpired += OnTargetExpired;
    }

    private void OnTargetExpired(SmithHitMesage message)
    {
        SendMessage(message);
    }

    private void OnGameEnded(int obj)
    {
        SendMessage(new SmithGameEnded());
    }

    private void OnStartGamePressed()
    {
        var state = State as SmithGameData;
        _window!.StartGame(state!);
        SendMessage(new ClientStartedGameEvent());
    }

    private void OnTargetHit(SmithHitMesage obj)
    {
        SendMessage(obj);
    }

    private void OnHitMissed()
    {
        var message = new SmithHitMesage(SmithHitState.Missed, false);
        SendMessage(message);
    }
}
