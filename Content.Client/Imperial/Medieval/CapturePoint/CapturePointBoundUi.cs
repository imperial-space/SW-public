using System.Globalization;
using Content.Shared.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.CapturePoint;

[UsedImplicitly]
public sealed class CapturePointBoundUi : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private CapturePointStartWindow? _window;

    public CapturePointBoundUi(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CapturePointStartWindow>();
        _window.StartButton.OnPressed += _ => OnStartButtonPressed();
    }

    private void OnStartButtonPressed()
    {
        SendMessage(new StartCapturePointMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not CapturePointBuiState cast)
            return;

        UpdateWindow(cast);
    }

    private void UpdateWindow(CapturePointBuiState state)
    {
        if (_window == null)
            return;

        var factionProto = _protoManager.Index(state.PlayerFaction);

        _window.FactionLabel.Text = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(factionProto.Name);
        _window.FactionLabel.FontColorOverride = factionProto.Color;

        _window.AlliesCountLabel.Text = state.NearbyAllies.Count.ToString();

        _window.AlliesListContainer.DisposeAllChildren();
        foreach (var allyName in state.NearbyAllies)
        {
            _window.AlliesListContainer.AddChild(new Label
            {
                Text = $"  ▸ {allyName}",
                FontColorOverride = new Color(0.75f, 0.68f, 0.50f),
                Margin = new Thickness(0, 1, 0, 1),
            });
        }

        var estMinutes = (int)(state.EstimatedDuration / 60);
        var estSeconds = (int)(state.EstimatedDuration % 60);
        _window.EstimatedTimeLabel.Text = Loc.GetString("medieval-capture-point-start-estimated-time",
            ("minutes", estMinutes.ToString("D2")), ("seconds", estSeconds.ToString("D2")));

        if (state is { CanStart: false, CannotStartReason: not null })
        {
            _window.CannotStartLabel.Visible = true;
            _window.CannotStartLabel.Text = state.CannotStartReason;
            _window.CannotStartLabel.FontColorOverride = new Color(0.85f, 0.2f, 0.2f);
        }
        else
        {
            _window.CannotStartLabel.Visible = false;
        }

        _window.StartButton.Disabled = !state.CanStart;

        if (EntMan.TryGetComponent<CapturePointComponent>(Owner, out var capturePoint))
            _window.PointNameLabel.Text = capturePoint.PointName;
    }
}
