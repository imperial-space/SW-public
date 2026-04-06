using Content.Shared.Imperial.Medieval.Courier;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Courier;

[UsedImplicitly]
public sealed class courierBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CourierMenu? _menu;

    public courierBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CourierMenu>();
        BindMenuEvents();
    }

    private void BindMenuEvents()
    {
        if (_menu == null)
            return;

        _menu.OnBuyOffer += (_, offerIndex) => SendMessage(new CourierBuyMessage(offerIndex));
        _menu.OnWithdrawAttempt += (_, _, amount) => SendMessage(new CourierRequestWithdrawMessage(amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null)
            return;

        if (state is not CourierUpdateState msg)
            return;

        _menu.UpdateState(msg);
    }
}
