using Content.Shared.Trade;

namespace Content.Client.Trade.UI;

public sealed class TradeTerminalBoundUserInterface : BoundUserInterface
{

    private TradeTerminalWindow? _window;

    public TradeTerminalBoundUserInterface(EntityUid owner, Enum uiKey)
        : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new TradeTerminalWindow();

        _window.OnCall       += target => SendMessage(new TradeCallMessage(target));
        _window.OnAccept     += ()     => SendMessage(new TradeAcceptCallMessage());
        _window.OnHangUp     += ()     => SendMessage(new TradeHangUpMessage());
        _window.OnConfirm    += ()     => SendMessage(new TradeConfirmMessage());
        _window.OnCancel     += ()     => SendMessage(new TradeCancelMessage());
        _window.OnRemoveItem += item   => SendMessage(new TradeRemoveItemMessage(item));

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is TradeBuiState tradeState)
            _window?.UpdateState(tradeState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
