using Content.Shared.Imperial.Medieval.Trading;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Trading;

[UsedImplicitly]
public sealed class TradingBoundUserInterface : BoundUserInterface
{
    private TradingMenu? _menu;
    private readonly TradingExamineSystem _examineSystem;
    private bool _isOwner;
    private bool _canBuy;

    public TradingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _examineSystem = EntMan.System<TradingExamineSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<TradingMenu>();
        _menu.OnBuy += commodity => SendPurchaseMessage(new TradingBuyMessage(commodity));
        _menu.OnSell += commodity => SendOwnerMessage(new TradingSellMessage(commodity));
        _menu.OnBuyOffer += offer => SendPurchaseMessage(new TradingBuyOfferMessage(offer));
        _menu.OnSellOffer += offer => SendOwnerMessage(new TradingSellOfferMessage(offer));
        _menu.OnSelectCommodity += commodity => SendMessage(new TradingSelectCommodityMessage(commodity));
        _menu.OnSelectOffer += offer => SendMessage(new TradingSelectOfferMessage(offer));
        _menu.OnCreateSellOffer += price => SendOwnerMessage(new TradingCreateSellOfferMessage(price));
        _menu.OnPrepareUnitSellOffer += price => SendOwnerMessage(new TradingPrepareUnitSellOfferMessage(price));
        _menu.OnCreateUnitSellOffers += (request, amount) =>
            SendOwnerMessage(new TradingCreateUnitSellOffersMessage(request, amount));
        _menu.OnCreateBuyOffer += (commodity, price) => SendOwnerMessage(new TradingCreateBuyOfferMessage(commodity, price));
        _menu.OnCreateBuyOfferFromHeld += price => SendOwnerMessage(new TradingCreateBuyOfferFromHeldMessage(price));
        _menu.OnCancelOffer += id => SendOwnerMessage(new TradingCancelOfferMessage(id));
        _menu.OnCollectStoredItem += item => SendOwnerMessage(new TradingCollectStoredItemMessage(item));
        _menu.OnCollectSaleRevenue += sale => SendOwnerMessage(new TradingCollectSaleRevenueMessage(sale));
        _menu.OnExamineItem += item =>
        {
            _examineSystem.Begin(Owner, EntMan.GetEntity(item));
            SendMessage(new TradingExamineItemMessage(item));
        };
        _menu.OnExamineCommodity += (commodity, product) =>
        {
            _examineSystem.Begin(Owner, _menu.GetPrototypeExamineEntity(product), commodity);
            SendMessage(new TradingExamineCommodityMessage(commodity));
        };
        _menu.OnWithdraw += amount => SendOwnerMessage(new TradingRequestWithdrawMessage(amount));
        SendMessage(new TradingRequestUpdateInterfaceMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is TradingUpdateState update)
        {
            _isOwner = update.IsOwner;
            _canBuy = update.IsOwner || update.IsPublic;
            _menu?.UpdateState(update);
        }
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is TradingUpdateInterfaceMessage update)
        {
            _isOwner = update.State.IsOwner;
            _canBuy = update.State.IsOwner || update.State.IsPublic;
            _menu?.UpdateState(update.State);
        }
        else if (message is TradingExamineInfoMessage examine)
        {
            if (_menu == null)
                return;

            var target = examine.PreviewProduct is { } product
                ? _menu.GetPrototypeExamineEntity(product)
                : EntMan.GetEntity(examine.Item);
            _examineSystem.Open(
                Owner,
                target,
                examine.Message,
                examine.Verbs,
                examine.CommodityId,
                verb => SendMessage(new TradingExecuteExamineVerbMessage(examine.Item, verb)));
        }
        else if (message is TradingUnitSellOfferPreparedMessage prepared)
        {
            _menu?.OpenUnitSellWindow(prepared);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _examineSystem.Close(Owner);
            _menu?.StopTrackingHands();
        }

        base.Dispose(disposing);
    }

    private void SendOwnerMessage(BoundUserInterfaceMessage message)
    {
        if (_isOwner)
            SendMessage(message);
    }

    private void SendPurchaseMessage(BoundUserInterfaceMessage message)
    {
        if (_canBuy)
            SendMessage(message);
    }
}
