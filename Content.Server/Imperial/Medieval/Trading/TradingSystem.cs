using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.MedievalMoneyChecker.Components;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;

    private EntityUid? _market;
    private CancellationTokenSource? _marketUpdateCancellation;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TradingComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<TradingComponent, EntityTerminatingEvent>(OnTradingPitTerminating);
        SubscribeLocalEvent<TradingLotBlockedComponent, ExaminedEvent>(OnTradingLotBlockedExamined);
        SubscribeLocalEvent<TradingLotBlockedComponent, StackSplitEvent>(OnTradingLotBlockedStackSplit);
        SubscribeLocalEvent<MedievalCurrencyComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        InitializeUi();
        InitializePublicTrading();
    }

    public override void Shutdown()
    {
        StopMarketUpdates();
        base.Shutdown();
    }

    private void OnTradingPitTerminating(
        Entity<TradingComponent> pit,
        ref EntityTerminatingEvent args)
    {
        if (HasComp<PublicTradingPitComponent>(pit.Owner))
            RemovePublicTradingSessions(pit.Owner);

        if (TryGetMarket(out var market))
        {
            var offers = market.Comp.Offers.Values
                .Where(offer => offer.Pit == pit.Owner)
                .Select(offer => offer.Id)
                .ToList();

            foreach (var offer in offers)
            {
                RemoveOffer(market, offer, false);
            }
        }

        pit.Comp.MarketOffers.Clear();
        pit.Comp.StoredMarketItems.Clear();

        if (_containers.TryGetContainer(pit.Owner, TradingComponent.MarketContainerId, out var container))
            _containers.EmptyContainer(container, true, Transform(pit.Owner).Coordinates);
    }

    private void OnRoundStart(RoundStartedEvent args)
    {
        StopMarketUpdates();
        if (!CreateMarket())
            return;

        _marketUpdateCancellation = new CancellationTokenSource();
        _ = RunMarketUpdatesAsync(_marketUpdateCancellation.Token);
    }

    private void OnRoundEnd(RoundEndedEvent args)
    {
        StopMarketUpdates();
        DeleteMarket();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        StopMarketUpdates();
        DeleteMarket();
    }

    private async Task RunMarketUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!TryGetMarket(out var market))
                    return;

                var config = _prototypeManager.Index(market.Comp.Config);
                var interval = TimeSpan.FromSeconds(config.StepInterval);
                await Timer.Delay(interval, cancellationToken).WaitAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetMarket(out market))
                    return;

                config = _prototypeManager.Index(market.Comp.Config);
                RunMarketStep(market, config);
                UpdateAllInterfaces(market);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void StopMarketUpdates()
    {
        if (_marketUpdateCancellation == null)
            return;

        _marketUpdateCancellation.Cancel();
        _marketUpdateCancellation.Dispose();
        _marketUpdateCancellation = null;
    }

    private void OnBeforeUiOpen(EntityUid uid, TradingComponent component, BeforeActivatableUIOpenEvent args)
    {
        _containers.EnsureContainer<Robust.Shared.Containers.Container>(uid, TradingComponent.MarketContainerId);
        OpenPublicTradingSession(uid, args.User);
    }

    internal bool IsTradingPitOwner(EntityUid user, TradingComponent component)
    {
        return component.AccountOwner is { } owner &&
               _mind.TryGetMind(user, out var mindId, out _) &&
               owner == mindId;
    }

    public bool BindTradingPit(Entity<TradingComponent?> pit, EntityUid trader)
    {
        if (HasComp<PublicTradingPitComponent>(pit.Owner) ||
            !Resolve(pit.Owner, ref pit.Comp) ||
            !_mind.TryGetMind(trader, out var mindId, out _))
        {
            return false;
        }

        pit.Comp.AccountOwner = mindId;
        _metadata.SetEntityName(pit.Owner, Loc.GetString("trading-personal-pit-name"));
        return true;
    }

    private void OnAfterInteract(EntityUid uid, MedievalCurrencyComponent component, AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            HasComp<PublicTradingPitComponent>(args.Target) ||
            !TryComp<TradingComponent>(args.Target, out var store))
            return;

        if (!TryAddCurrency((uid, component), (args.Target.Value, store)))
            return;

        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("store-currency-inserted", ("used", args.Used), ("target", args.Target)),
            args.Target.Value,
            args.User);
    }

    public bool TryAddCurrency(
        Entity<MedievalCurrencyComponent?> currency,
        Entity<TradingComponent?> store)
    {
        if (HasComp<PublicTradingPitComponent>(store.Owner) ||
            !Resolve(currency.Owner, ref currency.Comp) ||
            !Resolve(store.Owner, ref store.Comp))
            return false;

        var value = currency.Comp.Price;
        if (TryComp(currency.Owner, out StackComponent? stack) && stack.Count != 1)
        {
            value = currency.Comp.Price.ToDictionary(entry => entry.Key, entry => entry.Value * stack.Count);
        }

        if (!TryAddCurrency(value, store.Owner, store.Comp))
            return false;

        currency.Comp.Price.Clear();
        if (stack != null)
            _stack.SetCount(currency.Owner, 0, stack);

        QueueDel(currency.Owner);
        return true;
    }

    public bool TryAddCurrency(
        Dictionary<string, FixedPoint2> currency,
        EntityUid uid,
        TradingComponent? store = null)
    {
        if (HasComp<PublicTradingPitComponent>(uid) || !Resolve(uid, ref store))
            return false;

        foreach (var type in currency.Keys)
        {
            if (store.Currency != type)
                return false;
        }

        foreach (var value in currency.Values)
        {
            store.Balance += value.Int();
        }

        foreach (var user in _ui.GetActors(uid, TradingUiKey.Key))
        {
            UpdateUserInterface(user, uid, store);
        }
        return true;
    }

    private bool TryGetMarket(out Entity<TradingMarketComponent> market)
    {
        if (_market is { } marketUid &&
            !TerminatingOrDeleted(marketUid) &&
            !EntityManager.IsQueuedForDeletion(marketUid) &&
            TryComp<TradingMarketComponent>(marketUid, out var component))
        {
            market = (marketUid, component);
            return true;
        }

        var query = EntityQueryEnumerator<TradingMarketComponent>();
        while (query.MoveNext(out marketUid, out component))
        {
            if (TerminatingOrDeleted(marketUid) || EntityManager.IsQueuedForDeletion(marketUid))
                continue;

            _market = marketUid;
            market = (marketUid, component);
            return true;
        }

        market = default;
        return false;
    }
}
