using Content.Shared.Imperial.Medieval.MedievalMap;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.MedievalMap;


public sealed class MedievalMapBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MedievalMapWindow? _window;

    public MedievalMapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MedievalMapWindow>();
        _window.OpenCenteredRight();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MedievalMapBoundUiState msg) return;
        if (_window is null) return;

        _window.MinSize = msg.Size;
        _window.MaxSize = msg.Size;
        _window.SetSize = msg.Size;

        _window.UpdateBackground(msg.MapTexturePath);
    }
}
