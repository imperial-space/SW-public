using System.Linq;
using Content.Server.Store.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Stacks;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Trading;

/// <summary>
/// Player listings, instant sell and order fulfilment for public trading pits.
/// </summary>
public sealed partial class TradingSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly string[] MerchantJobs = { "MedievalTrader", "MedievalTraderN" };

    private GameTick _merchantOnlineTick;
    private bool _merchantOnline;

    private void InitializePublicListings()
    {
        SubscribeLocalEvent<PublicTradingPitComponent, AfterInteractUsingEvent>(OnInsertStagedItem);
        SubscribeLocalEvent<TradingComponent, TradingListStagedItemMessage>(OnListStagedItem);
        SubscribeLocalEvent<TradingComponent, TradingSellStagedItemMessage>(OnSellStagedItem);
        SubscribeLocalEvent<TradingComponent, TradingFulfillStagedItemMessage>(OnFulfillStagedItem);
        SubscribeLocalEvent<TradingComponent, TradingWithdrawStagedItemMessage>(OnWithdrawStagedItem);
        SubscribeLocalEvent<TradingComponent, TradingBuyPublicListingMessage>(OnBuyPublicListing);
        SubscribeLocalEvent<TradingComponent, TradingCancelPublicListingMessage>(OnCancelPublicListing);
        SubscribeLocalEvent<TradingComponent, TradingCollectPublicSaleRevenueMessage>(OnCollectPublicSaleRevenue);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(_ => OnMerchantPresenceChanged());
        _playerManager.PlayerStatusChanged += (_, _) => OnMerchantPresenceChanged();
    }

    private void OnMerchantPresenceChanged()
    {
        _merchantOnlineTick = default;
        if (TryGetMarket(out var market))
            UpdateAllInterfaces(market);
    }

    /// <summary>
    /// Merchants can always list; everyone else is blocked while any merchant is online.
    /// </summary>
    private bool IsListingBlockedFor(EntityUid actor)
    {
        return !IsMerchant(actor) && IsAnyMerchantOnline();
    }

    private bool IsMerchant(EntityUid actor)
    {
        return _mind.TryGetMind(actor, out var mindId, out _) && IsMerchantMind(mindId);
    }

    private bool IsMerchantMind(EntityUid mindId)
    {
        return _jobs.MindTryGetJob(mindId, out var job) && MerchantJobs.Contains(job.ID);
    }

    private bool IsAnyMerchantOnline()
    {
        if (_merchantOnlineTick == _timing.CurTick)
            return _merchantOnline;

        _merchantOnline = false;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status is SessionStatus.Connected or SessionStatus.InGame &&
                _mind.TryGetMind(session, out var mindId, out _) &&
                IsMerchantMind(mindId))
            {
                _merchantOnline = true;
                break;
            }
        }

        _merchantOnlineTick = _timing.CurTick;
        return _merchantOnline;
    }

    private bool IsAlive(EntityUid item)
    {
        return Exists(item) && !TerminatingOrDeleted(item) && !EntityManager.IsQueuedForDeletion(item);
    }

    private bool TryGetPublicListingBoard(out Entity<TradingMarketComponent> market, out PublicListingBoardComponent board)
    {
        if (TryGetMarket(out market) && TryComp<PublicListingBoardComponent>(market.Owner, out var boardComp))
        {
            board = boardComp;
            return true;
        }

        board = default!;
        return false;
    }

    private bool TryGetOwnedStaged(
        EntityUid pit,
        EntityUid actor,
        Guid stagedId,
        out Entity<TradingMarketComponent> market,
        out PublicListingBoardComponent board,
        out PublicStagedItem staged)
    {
        market = default;
        board = default!;
        staged = default!;

        return HasComp<PublicTradingPitComponent>(pit) &&
               TryGetPublicListingBoard(out market, out board) &&
               board.StagedItems.TryGetValue(stagedId, out staged!) &&
               _mind.TryGetMind(actor, out var mindId, out _) &&
               mindId == staged.OwnerMindId;
    }

    private bool DiscardIfDead(
        Entity<TradingMarketComponent> market,
        PublicListingBoardComponent board,
        PublicStagedItem staged,
        EntityUid actor)
    {
        if (IsAlive(staged.Item))
            return false;

        board.StagedItems.Remove(staged.Id);
        ShowInvalidSellOffer(actor);
        UpdateAllInterfaces(market);
        return true;
    }

    private bool TryMoveToContainer(EntityUid item, EntityUid owner, string containerId)
    {
        var destination = _containers.EnsureContainer<Container>(owner, containerId);
        if (_containers.TryGetContainingContainer((item, null, null), out var previous) &&
            !_containers.Remove(item, previous, reparent: false, force: true))
        {
            return false;
        }

        if (_containers.Insert(item, destination, force: true))
            return true;

        if (previous != null)
            _containers.Insert(item, previous, force: true);

        return false;
    }

    private void PayCurrency(EntityUid actor, TradingComponent pit, int amount)
    {
        if (_prototypeManager.TryIndex(pit.Currency, out var currency) && currency.Cash != null)
            SpawnCurrencyFor(actor, currency.Cash, amount);
    }

    private void OnInsertStagedItem(EntityUid uid, PublicTradingPitComponent component, AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != uid || !TryGetPublicListingBoard(out var market, out var board))
            return;

        if (!_mind.TryGetMind(args.User, out var ownerMindId, out _))
            return;

        var item = args.Used;
        var config = _prototypeManager.Index(market.Comp.Config);
        if (!CanTradeItem(item, config))
        {
            ShowInvalidSellOffer(args.User);
            return;
        }

        if (!TryMoveToContainer(item, market.Owner, PublicListingBoardComponent.StagingContainerId))
        {
            ShowInvalidSellOffer(args.User);
            return;
        }

        var staged = new PublicStagedItem
        {
            Id = Guid.NewGuid(),
            Item = item,
            Product = MetaData(item).EntityPrototype?.ID ?? string.Empty,
            DisplayName = MetaData(item).EntityName,
            OwnerMindId = ownerMindId,
            SellerName = MetaData(args.User).EntityName,
            Sequence = board.NextSequence++,
        };
        board.StagedItems.Add(staged.Id, staged);

        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("trading-ui-public-item-inserted", ("item", staged.DisplayName)),
            uid,
            args.User);
        UpdateAllInterfaces(market);
    }

    private void OnListStagedItem(EntityUid uid, TradingComponent component, TradingListStagedItemMessage msg)
    {
        if (!TryGetOwnedStaged(uid, msg.Actor, msg.StagedItemId, out var market, out var board, out var staged))
            return;

        if (IsListingBlockedFor(msg.Actor))
        {
            _popup.PopupCursor(Loc.GetString("trading-ui-merchant-online-blocked"), msg.Actor, PopupType.SmallCaution);
            UpdateUserInterface(msg.Actor, uid, component);
            return;
        }

        if (msg.Price <= 0)
        {
            ShowInvalidSellOffer(msg.Actor);
            return;
        }

        if (DiscardIfDead(market, board, staged, msg.Actor))
            return;

        if (!TryMoveStagedItemToListing(market, board, staged, msg.Price))
        {
            ShowInvalidSellOffer(msg.Actor);
            return;
        }

        board.StagedItems.Remove(msg.StagedItemId);
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-public-listing-created");
        UpdateAllInterfaces(market);
    }

    private void OnSellStagedItem(EntityUid uid, TradingComponent component, TradingSellStagedItemMessage msg)
    {
        if (!TryGetOwnedStaged(uid, msg.Actor, msg.StagedItemId, out var market, out var board, out var staged) ||
            DiscardIfDead(market, board, staged, msg.Actor))
        {
            return;
        }

        if (GetInstantSellValue(staged.Item, component.Currency) is not { } payout)
        {
            ShowInvalidSellOffer(msg.Actor);
            return;
        }

        board.StagedItems.Remove(msg.StagedItemId);
        QueueDel(staged.Item);
        PayCurrency(msg.Actor, component, payout);

        ShowTradingSuccessMessage(
            msg.Actor,
            uid,
            component,
            Loc.GetString("trading-ui-public-instant-sell-success", ("amount", payout)));
        UpdateAllInterfaces(market);
    }

    /// <summary>
    /// Sells a staged item to the highest matching merchant buy order.
    /// </summary>
    private void OnFulfillStagedItem(EntityUid uid, TradingComponent component, TradingFulfillStagedItemMessage msg)
    {
        if (!TryGetOwnedStaged(uid, msg.Actor, msg.StagedItemId, out var market, out var board, out var staged) ||
            DiscardIfDead(market, board, staged, msg.Actor))
        {
            return;
        }

        var config = _prototypeManager.Index(market.Comp.Config);
        if (!CanTradeItem(staged.Item, config) || !TryFindBestBuyOffer(market, staged.Item, out var bid))
        {
            ShowInvalidSellOffer(msg.Actor);
            return;
        }

        board.StagedItems.Remove(msg.StagedItemId);

        var item = staged.Item;
        if (bid.Pit is { } destination && TryComp<TradingComponent>(destination, out var destinationPit))
            DeliverItem(destination, destinationPit, item, bid.ImmediateRecipient);
        else
            QueueDel(item);

        RemoveOfferRecord(market, bid);
        if (market.Comp.Commodities.TryGetValue(bid.CommodityId, out var commodity))
            TryRemoveCommodity(market, commodity);

        PayCurrency(msg.Actor, component, bid.Price);

        ShowTradingSuccessMessage(
            msg.Actor,
            uid,
            component,
            Loc.GetString("trading-ui-public-fulfill-success", ("amount", bid.Price)));
        UpdateAllInterfaces(market);
    }

    /// <summary>
    /// Finds the highest merchant buy order for the item (Unique commodities only).
    /// </summary>
    private bool TryFindBestBuyOffer(Entity<TradingMarketComponent> market, EntityUid item, out TradingMarketOffer bid)
    {
        bid = default!;
        if (!TryResolveCommodityForItem(market, item, 1, false, out var commodity) ||
            (commodity.Sections & TradingMarketSection.Unique) == 0)
        {
            return false;
        }

        var best = market.Comp.Offers.Values
            .Where(offer => offer.CommodityId == commodity.Id &&
                            offer.Side == TradingOfferSide.Buy &&
                            offer.ParticipantKind == TradingParticipantKind.Trader)
            .OrderByDescending(offer => offer.Price)
            .ThenBy(offer => offer.Sequence)
            .FirstOrDefault();

        if (best == null)
            return false;

        bid = best;
        return true;
    }

    private void OnWithdrawStagedItem(EntityUid uid, TradingComponent component, TradingWithdrawStagedItemMessage msg)
    {
        if (!TryGetOwnedStaged(uid, msg.Actor, msg.StagedItemId, out var market, out var board, out var staged))
            return;

        board.StagedItems.Remove(msg.StagedItemId);
        if (IsAlive(staged.Item))
            _delivery.Deliver(staged.Item, msg.Actor);

        UpdateAllInterfaces(market);
    }

    private void OnBuyPublicListing(EntityUid uid, TradingComponent component, TradingBuyPublicListingMessage msg)
    {
        if (!HasComp<PublicTradingPitComponent>(uid) ||
            !TryGetPublicListingBoard(out var market, out var board) ||
            !board.Listings.TryGetValue(msg.ListingId, out var listing))
        {
            return;
        }

        if (!_mind.TryGetMind(msg.Actor, out var buyerMindId, out _) || buyerMindId == listing.SellerMindId)
            return;

        if (!IsAlive(listing.Item))
        {
            board.Listings.Remove(msg.ListingId);
            UpdateAllInterfaces(market);
            return;
        }

        if (!TrySpendPublicCurrency(msg.Actor, component.Currency, listing.Price))
        {
            ShowInsufficientPurchaseFunds(msg.Actor);
            UpdateUserInterface(msg.Actor, uid, component);
            return;
        }

        board.Listings.Remove(msg.ListingId);
        _delivery.Deliver(listing.Item, msg.Actor);

        var sales = board.PendingSalesByMind.GetOrNew(listing.SellerMindId);
        sales.Add(new PublicPendingSale
        {
            Id = Guid.NewGuid(),
            ItemName = listing.DisplayName,
            BuyerName = MetaData(msg.Actor).EntityName,
            Price = listing.Price,
        });

        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-purchase-success");
        UpdateAllInterfaces(market);
    }

    private void OnCancelPublicListing(EntityUid uid, TradingComponent component, TradingCancelPublicListingMessage msg)
    {
        if (!HasComp<PublicTradingPitComponent>(uid) ||
            !TryGetPublicListingBoard(out var market, out var board) ||
            !board.Listings.TryGetValue(msg.ListingId, out var listing))
        {
            return;
        }

        if (!_mind.TryGetMind(msg.Actor, out var mindId, out _) || mindId != listing.SellerMindId)
            return;

        board.Listings.Remove(msg.ListingId);
        if (IsAlive(listing.Item))
            _delivery.Deliver(listing.Item, msg.Actor);

        UpdateAllInterfaces(market);
    }

    private void OnCollectPublicSaleRevenue(
        EntityUid uid,
        TradingComponent component,
        TradingCollectPublicSaleRevenueMessage msg)
    {
        if (!HasComp<PublicTradingPitComponent>(uid) ||
            !TryGetPublicListingBoard(out var market, out var board) ||
            !_mind.TryGetMind(msg.Actor, out var mindId, out _) ||
            !board.PendingSalesByMind.TryGetValue(mindId, out var sales))
        {
            return;
        }

        var index = sales.FindIndex(sale => sale.Id == msg.SaleId);
        if (index < 0)
            return;

        var sale = sales[index];
        sales.RemoveAt(index);
        if (sales.Count == 0)
            board.PendingSalesByMind.Remove(mindId);

        PayCurrency(msg.Actor, component, sale.Price);

        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-sale-revenue-collected");
        UpdateAllInterfaces(market);
    }

    /// <summary>
    /// Half of the value the appraiser trait shows, or null if the item has none.
    /// </summary>
    private int? GetInstantSellValue(EntityUid item, string currency)
    {
        return GetCurrencyValue(item, currency) is { } value ? Math.Max(1, (int) (value / 2)) : null;
    }

    private double? GetCurrencyValue(EntityUid item, string currency)
    {
        var count = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;

        if (TryComp<CurrencyComponent>(item, out var store) &&
            store.Price.TryGetValue(currency, out var price) && price != 1)
            return (double) price * count;

        if (TryComp<MedievalCurrencyComponent>(item, out var medieval) &&
            medieval.Price.TryGetValue(currency, out var medievalPrice) && medievalPrice != 1)
            return (double) medievalPrice * count;

        return null;
    }

    private bool TryMoveStagedItemToListing(
        Entity<TradingMarketComponent> market,
        PublicListingBoardComponent board,
        PublicStagedItem staged,
        int price)
    {
        var item = staged.Item;
        var config = _prototypeManager.Index(market.Comp.Config);
        if (!CanTradeItem(item, config))
            return false;

        if (!TryMoveToContainer(item, market.Owner, PublicListingBoardComponent.ListingContainerId))
            return false;

        var listing = new PublicListing
        {
            Id = Guid.NewGuid(),
            Item = item,
            Product = staged.Product,
            DisplayName = staged.DisplayName,
            Price = price,
            SellerMindId = staged.OwnerMindId,
            SellerName = staged.SellerName,
            Sequence = board.NextSequence++,
        };
        board.Listings.Add(listing.Id, listing);
        return true;
    }

    private void SpawnCurrencyFor(EntityUid recipient, Dictionary<FixedPoint2, string> cash, int amount)
    {
        FixedPoint2 remaining = amount;
        var coordinates = Transform(recipient).Coordinates;
        foreach (var value in cash.Keys.OrderByDescending(value => value))
        {
            var amountToSpawn = (int) MathF.Floor((float) (remaining / value));
            if (amountToSpawn <= 0)
                continue;

            var entities = _stack.SpawnMultiple(cash[value], amountToSpawn, coordinates);
            foreach (var entity in entities)
            {
                _delivery.Deliver(entity, recipient);
            }

            remaining -= value * amountToSpawn;
        }
    }

    private void BuildPublicListingsState(
        Entity<TradingMarketComponent> market,
        EntityUid user,
        bool isPublic,
        string currency,
        out List<PublicListingState> publicListings,
        out List<PublicPendingSaleState> publicPendingSales,
        out List<PublicStagedItemState> publicStagedItems)
    {
        publicListings = new List<PublicListingState>();
        publicPendingSales = new List<PublicPendingSaleState>();
        publicStagedItems = new List<PublicStagedItemState>();

        if (!isPublic || !TryComp<PublicListingBoardComponent>(market.Owner, out var board))
            return;

        _mind.TryGetMind(user, out var userMindId, out _);

        publicListings = board.Listings.Values
            .OrderBy(listing => listing.Sequence)
            .Select(listing => new PublicListingState(
                listing.Id,
                listing.Product,
                listing.DisplayName,
                listing.Price,
                listing.SellerName,
                userMindId != default && listing.SellerMindId == userMindId,
                Exists(listing.Item) ? GetNetEntity(listing.Item) : null))
            .ToList();

        if (userMindId != default && board.PendingSalesByMind.TryGetValue(userMindId, out var sales))
        {
            publicPendingSales = sales
                .Select(sale => new PublicPendingSaleState(sale.Id, sale.ItemName, sale.BuyerName, sale.Price))
                .ToList();
        }

        if (userMindId == default)
            return;

        publicStagedItems = board.StagedItems.Values
            .Where(staged => staged.OwnerMindId == userMindId)
            .OrderBy(staged => staged.Sequence)
            .Select(staged => new PublicStagedItemState(
                staged.Id,
                staged.Product,
                staged.DisplayName,
                Exists(staged.Item) ? GetNetEntity(staged.Item) : null,
                GetInstantSellValue(staged.Item, currency) ?? 0,
                Exists(staged.Item) && TryFindBestBuyOffer(market, staged.Item, out var bid) ? bid.Price : null))
            .ToList();
    }
}
