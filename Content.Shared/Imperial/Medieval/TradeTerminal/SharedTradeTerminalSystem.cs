using Robust.Shared.Containers;

namespace Content.Shared.Trade;

public abstract class SharedTradeTerminalSystem : EntitySystem
{
    [Dependency] protected readonly SharedContainerSystem Containers = default!;

    protected Container GetOfferContainer(EntityUid uid, TradeTerminalComponent comp)
        => (Container) Containers.GetContainer(uid, comp.ContainerId);

    public bool IsAvailable(TradeTerminalComponent comp)
        => comp.State == TradeSessionState.Idle;


    public bool CanInsert(TradeTerminalComponent comp)
        => comp.State == TradeSessionState.Active;
}
