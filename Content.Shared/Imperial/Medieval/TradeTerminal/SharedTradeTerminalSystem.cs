using Robust.Shared.Containers;

namespace Content.Shared.Trade;

public abstract class SharedTradeTerminalSystem : EntitySystem
{
    [Dependency] protected readonly SharedContainerSystem Containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TradeTerminalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TradeTerminalComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(EntityUid uid, TradeTerminalComponent comp, ComponentInit args)
    {
    }

    private void OnShutdown(EntityUid uid, TradeTerminalComponent comp, ComponentShutdown args)
    {

        if (comp.LinkedTerminal is { } partner &&
            TryComp<TradeTerminalComponent>(partner, out var partnerComp))
        {
            partnerComp.LinkedTerminal = null;
        }
    }


    protected Container GetOfferContainer(EntityUid uid, TradeTerminalComponent comp)
        => (Container) Containers.GetContainer(uid, comp.ContainerId);

    public bool IsAvailable(TradeTerminalComponent comp)
        => comp.State == TradeSessionState.Idle;


    public bool CanInsert(TradeTerminalComponent comp)
        => comp.State == TradeSessionState.Active;
}
