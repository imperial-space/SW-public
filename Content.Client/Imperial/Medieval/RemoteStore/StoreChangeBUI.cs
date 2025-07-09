using Content.Shared.Imperial.Medieval.RemoteStore;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.RemoteStore;

public sealed class StoreChangeBUI: BoundUserInterface
{
    private StoreChangerWindow? _window;

    public StoreChangeBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open(); // НЕНАВИЖУ
        _window = this.CreateWindow<StoreChangerWindow>();
        _window.OnStoreClicked += uid => { SendMessage(new ChangeStoreMessage(uid)); };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        switch (state)
        {
            case RemoteStoreUIState remoteStoreState:
                _window?.UpdateStores(remoteStoreState);
                break;
        }
    }
}
