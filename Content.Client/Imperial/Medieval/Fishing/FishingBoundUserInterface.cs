using Content.Shared.Fishing.Bui;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Fishing;

public sealed class FishingBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private FishingMinigameControl? _control;

    public FishingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _control = this.CreateDisposableControl<FishingMinigameControl>();
        _control.LmbStateChanged += OnLmbStateChanged;

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

        SendMessage(new FishingMinigameInputMessage(_control.HoldingLmb));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_control == null || state is not FishingMinigameBoundUserInterfaceState cast)
            return;

        _control.SetValues(cast.Tension, cast.Progress);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _control != null)
            _control.LmbStateChanged -= OnLmbStateChanged;

        base.Dispose(disposing);
    }

    private void OnLmbStateChanged(bool held)
    {
        SendMessage(new FishingMinigameInputMessage(held));
    }
}
