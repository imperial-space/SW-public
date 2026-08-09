using Content.Shared.Imperial.Medieval.FactionFlag.UI;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.FactionFlag.UI;

public sealed class MedievalFactionFlagBoundUserInterface : BoundUserInterface
{
    private MedievalFactionFlagWindow? _window;

    public MedievalFactionFlagBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MedievalFactionFlagWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState buiState)
    {
        base.UpdateState(buiState);

        if (buiState is not MedievalFactionFlagBuiState state)
            return;

        _window?.SetPoints(state.Points);
    }
}
