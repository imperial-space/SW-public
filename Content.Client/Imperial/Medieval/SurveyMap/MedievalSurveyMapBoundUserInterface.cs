using Content.Shared.Imperial.Medieval.SurveyMap;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.SurveyMap;


public sealed class MedievalSurveyMapBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [ViewVariables]
    private MedievalSurveyMapWindow? _window;

    public MedievalSurveyMapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MedievalSurveyMapWindow>();
        _window.OnAnnotate += title => SendMessage(new MedievalSurveyMapAnnotateMessage(title));
        _window.OpenCenteredRight();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MedievalSurveyMapState msg) return;
        if (_window is null) return;

        _window.MinSize = msg.Size;
        _window.MaxSize = msg.Size;
        _window.SetSize = msg.Size;

        _window.UpdateAnnotations(msg, msg.Surveyor != null &&
            msg.Surveyor == EntMan.GetNetEntity(_players.LocalEntity));
    }
}
