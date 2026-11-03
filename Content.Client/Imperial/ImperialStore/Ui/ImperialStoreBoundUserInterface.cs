using Content.Client.Imperial.ImperialStore.Ui.Foliant;
using Content.Shared.Imperial.ImperialStore;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.ImperialStore.Ui;


public sealed class ImperialStoreBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private FoliantMenu? _menu;

    public ImperialStoreBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        // imperial medieval - the grimoire window, see FoliantMenu
        _menu = this.CreateWindow<FoliantMenu>();

        if (EntMan.TryGetComponent<ImperialStoreComponent>(Owner, out var store))
            _menu.SetStore(store);

        _menu.OnBuy += listing => SendMessage(new ImperialStoreBuyListingMessage(listing));
        _menu.OnSetSlot += (slot, school) => SendMessage(new ImperialStoreSetSlotMessage(slot, school));
        _menu.OnWithdraw += (type, amount) => SendMessage(new ImperialStoreRequestWithdrawMessage(type, amount));
        _menu.OnRefund += () => SendMessage(new ImperialStoreRequestRefundMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case ImperialStoreUpdateState msg:
                _menu?.UpdateState(msg);
                break;
        }
    }
}
