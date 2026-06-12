using Content.Shared.Fishing.Bui;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Fishing;

public sealed class FishingBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private FishingMinigameControl? _control;
    private FishingMinigameBoundUserInterfaceState? _pendingState;

    public FishingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _control = this.CreateDisposableControl<FishingMinigameControl>();
        _control.MinigameFinished += OnMinigameFinished;

        Control parent = _uiManager.WindowRoot;
        if (_uiManager.ActiveScreen != null)
        {
            try
            {
                parent = _uiManager.ActiveScreen.FindControl<LayoutContainer>("ViewportContainer");
            }
            catch (ArgumentException)
            {
                // Non-standard screen, keep fallback to WindowRoot.
            }
        }

        parent.AddChild(_control);
        EntMan.System<UserInterfaceSystem>().RegisterControl(this, _control);

        if (_pendingState != null)
            _control.StartMinigame(_pendingState);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not FishingMinigameBoundUserInterfaceState cast)
            return;

        _pendingState = cast;

        if (_control == null)
            return;

        _control.StartMinigame(cast);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is not FishingMinigameStopMessage)
            return;

        _control?.StopMinigameFromServer();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _control != null)
            _control.MinigameFinished -= OnMinigameFinished;

        base.Dispose(disposing);
    }

    private void OnMinigameFinished(FishingMinigameResult result)
    {
        SendMessage(new FishingMinigameResultMessage(result));
    }
}
