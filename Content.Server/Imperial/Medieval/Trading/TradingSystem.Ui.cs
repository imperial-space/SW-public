using System.Linq;
using Content.Server.Verbs;
using Content.Shared.FixedPoint;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Store;
using Content.Shared.Storage;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly VerbSystem _verbSystem = default!;
    [Dependency] private readonly TradingItemDeliverySystem _delivery = default!;

    private void InitializeUi()
    {
        Subs.BuiEvents<TradingComponent>(TradingUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });
        SubscribeLocalEvent<TradingComponent, TradingRequestUpdateInterfaceMessage>(OnRequestUpdate);
        SubscribeLocalEvent<TradingComponent, TradingBuyMessage>(OnBuyRequest);
        SubscribeLocalEvent<TradingComponent, TradingSellMessage>(OnSellRequest);
        SubscribeLocalEvent<TradingComponent, TradingBuyOfferMessage>(OnBuyOfferRequest);
        SubscribeLocalEvent<TradingComponent, TradingSellOfferMessage>(OnSellOfferRequest);
        SubscribeLocalEvent<TradingComponent, TradingSelectCommodityMessage>(OnSelectCommodity);
        SubscribeLocalEvent<TradingComponent, TradingSelectOfferMessage>(OnSelectOffer);
        SubscribeLocalEvent<TradingComponent, TradingCreateSellOfferMessage>(OnCreateSellOffer);
        SubscribeLocalEvent<TradingComponent, TradingPrepareUnitSellOfferMessage>(OnPrepareUnitSellOffer);
        SubscribeLocalEvent<TradingComponent, TradingCreateUnitSellOffersMessage>(OnCreateUnitSellOffers);
        SubscribeLocalEvent<TradingComponent, TradingCreateBuyOfferMessage>(OnCreateBuyOffer);
        SubscribeLocalEvent<TradingComponent, TradingCreateBuyOfferFromHeldMessage>(OnCreateBuyOfferFromHeld);
        SubscribeLocalEvent<TradingComponent, TradingCancelOfferMessage>(OnCancelOffer);
        SubscribeLocalEvent<TradingComponent, TradingCollectStoredItemMessage>(OnCollectStoredItem);
        SubscribeLocalEvent<TradingComponent, TradingCollectSaleRevenueMessage>(OnCollectSaleRevenue);
        SubscribeLocalEvent<TradingComponent, TradingExamineItemMessage>(OnExamineItem);
        SubscribeLocalEvent<TradingComponent, TradingExamineCommodityMessage>(OnExamineCommodity);
        SubscribeLocalEvent<TradingComponent, TradingExecuteExamineVerbMessage>(OnExecuteExamineVerb);
        SubscribeLocalEvent<TradingComponent, TradingRequestWithdrawMessage>(OnRequestWithdraw);
        SubscribeLocalEvent<TradingComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
    }

    public void CloseUi(EntityUid uid, TradingComponent? component = null)
    {
        if (Resolve(uid, ref component))
            _ui.CloseUi(uid, TradingUiKey.Key);
    }

    public void UpdateUserInterface(EntityUid user, EntityUid store, TradingComponent? component = null)
    {
        if (!Resolve(store, ref component) || !TryGetMarket(out var market))
            return;

        var isPublic = HasComp<PublicTradingPitComponent>(store);
        var isOwner = !isPublic && IsTradingPitOwner(user, component);
        component.MarketOffers.RemoveWhere(id => !market.Comp.Offers.ContainsKey(id));
        component.StoredMarketItems.RemoveAll(item => !Exists(item));
        var viewer = EnsureComp<TradingMarketViewerComponent>(user);
        var visibleOffers = market.Comp.Offers.Values
            .Where(offer => !isPublic ||
                            offer.Side == TradingOfferSide.Sell &&
                            offer.ParticipantKind == TradingParticipantKind.Trader)
            .ToList();
        var publicCommodities = isPublic
            ? visibleOffers.Select(offer => offer.CommodityId).ToHashSet()
            : new HashSet<Guid>();
        if (isPublic &&
            (viewer.SelectedCommodity is not { } publicSelection ||
             !publicCommodities.Contains(publicSelection)))
        {
            viewer.SelectedCommodity = market.Comp.Commodities.Values
                .FirstOrDefault(commodity => publicCommodities.Contains(commodity.Id))?.Id;
            viewer.SelectedOffer = null;
        }
        else if (!isOwner && !isPublic &&
            (viewer.SelectedCommodity is not { } selected ||
             !market.Comp.Commodities.TryGetValue(selected, out var selectedItem) ||
             (selectedItem.Sections & TradingMarketSection.Unique) == 0))
        {
            viewer.SelectedCommodity = market.Comp.Commodities.Values
                .FirstOrDefault(commodity => (commodity.Sections & TradingMarketSection.Unique) != 0)?.Id;
            viewer.SelectedOffer = null;
        }

        if (isPublic &&
            viewer.SelectedOffer is { } selectedOffer &&
            (!market.Comp.Offers.TryGetValue(selectedOffer, out var publicOffer) ||
             publicOffer.Side != TradingOfferSide.Sell ||
             publicOffer.ParticipantKind != TradingParticipantKind.Trader ||
             publicOffer.CommodityId != viewer.SelectedCommodity))
        {
            viewer.SelectedOffer = null;
        }

        RefreshVisibleMarketItems(user, store, component, market, isOwner, isPublic);
        var selectedCommodity = viewer.SelectedCommodity;
        var offersByCommodity = visibleOffers.ToLookup(offer => offer.CommodityId);

        var items = market.Comp.Commodities.Values
            .Where(commodity => isPublic
                ? publicCommodities.Contains(commodity.Id)
                : isOwner || (commodity.Sections & TradingMarketSection.Unique) != 0)
            .Select(commodity =>
            {
                var commodityOffers = offersByCommodity[commodity.Id].ToList();
                var asks = commodityOffers.Where(offer => offer.Side == TradingOfferSide.Sell).ToList();
                var bids = commodityOffers
                    .Where(offer => offer.Side == TradingOfferSide.Buy && (!isOwner || offer.Pit != store))
                    .ToList();
                var traderBids = bids
                    .Where(offer => offer.ParticipantKind == TradingParticipantKind.Trader)
                    .ToList();
                var lowestSellOffer = isPublic
                    ? GetLowestPublicSellOffer(commodityOffers, commodity.Id)
                    : GetLowestSellOffer(
                        commodityOffers,
                        commodity.Id,
                        isOwner ? store : null);
                var preview = lowestSellOffer?.Item;
                var displayName = commodity.DisplayName;
                var description = commodity.Description;
                int? stackCount = commodity.HasStack ? commodity.BaselineStackCount : null;
                var damagedEquipment = commodity.IsDamagedEquipment;
                NetEntity? previewEntity = null;
                if (preview is { } previewItem && Exists(previewItem))
                {
                    var metadata = MetaData(previewItem);
                    stackCount = TryComp<StackComponent>(previewItem, out var stack) ? stack.Count : null;
                    displayName = FormatStackName(metadata.EntityName, stackCount);
                    description = metadata.EntityDescription;
                    damagedEquipment = IsDamagedEquipment(previewItem);
                    previewEntity = GetNetEntity(previewItem);
                }

                return new TradingMarketItemState(
                    commodity.Id,
                    commodity.Product,
                    commodity.Sections,
                    displayName,
                    description,
                    stackCount,
                    previewEntity,
                    commodity.Permanent,
                    commodity.HasStack,
                    commodity.BaselineStackCount,
                    damagedEquipment,
                    lowestSellOffer?.Price,
                    traderBids.Count == 0 ? null : traderBids.Max(offer => offer.Price),
                    asks.Count,
                    bids.Count,
                    new HashSet<ProtoId<GuildTypePrototype>>(commodity.Categories));
            })
            .ToList();

        var offers = visibleOffers
            .Where(offer => offer.CommodityId == selectedCommodity || isOwner && offer.Pit == store)
            .OrderBy(offer => offer.Product.Id)
            .ThenBy(offer => offer.Side)
            .ThenBy(offer => offer.Price)
            .Select(offer => CreateOfferState(market, store, offer, isOwner))
            .ToList();

        var storedItems = (isOwner ? component.StoredMarketItems : [])
            .Where(item => Exists(item) && MetaData(item).EntityPrototype != null)
            .Select(item =>
            {
                var metadata = MetaData(item);
                var product = metadata.EntityPrototype!.ID;
                var stackCount = TryComp<StackComponent>(item, out var stack) ? stack.Count : (int?) null;
                return new TradingStoredItemState(
                    GetNetEntity(item),
                    product,
                    FormatStackName(metadata.EntityName, stackCount));
            })
            .ToList();

        var pendingSales = (isOwner ? component.PendingSales : [])
            .Select(sale => new TradingPendingSaleState(
                sale.Id,
                sale.ItemName,
                sale.BuyerName,
                sale.Price))
            .ToList();

        _ui.ServerSendUiMessage(
            store,
            TradingUiKey.Key,
            new TradingUpdateInterfaceMessage(
                new TradingUpdateState(
                    items,
                    offers,
                    storedItems,
                    pendingSales,
                    isOwner ? new List<string>(component.MarketArchive) : [],
                    isPublic
                        ? GetPublicTradingBalance(user, store, component.Currency)
                        : isOwner ? component.Balance : 0,
                    component.Currency,
                    isOwner,
                    isPublic)),
            user);
    }

    private void OnUiMessageAttempt(
        Entity<TradingComponent> pit,
        ref BoundUserInterfaceMessageAttempt args)
    {
        var isPublic = HasComp<PublicTradingPitComponent>(pit.Owner);
        if (IsTradingPitOwner(args.Actor, pit.Comp) ||
            isPublic && args.Message is (TradingBuyMessage or TradingBuyOfferMessage) ||
            args.Message is OpenBoundInterfaceMessage or
                TradingRequestUpdateInterfaceMessage or
                TradingSelectCommodityMessage or
                TradingSelectOfferMessage or
                TradingExamineItemMessage or
                TradingExamineCommodityMessage or
                TradingExecuteExamineVerbMessage)
        {
            return;
        }

        args.Cancel();
    }

    private void OnExamineItem(EntityUid uid, TradingComponent component, TradingExamineItemMessage args)
    {
        if (!TryGetEntity(args.Item, out var item) ||
            !Exists(item) ||
            !TryComp<TradingMarketViewerComponent>(args.Actor, out var viewer) ||
            !viewer.VisibleItems.Contains(item.Value))
        {
            return;
        }

        SendExamineInfo(uid, args.Actor, item.Value);
    }

    private void OnExamineCommodity(
        EntityUid uid,
        TradingComponent component,
        TradingExamineCommodityMessage args)
    {
        if (!TryGetMarket(out var market) ||
            !market.Comp.Commodities.TryGetValue(args.CommodityId, out var commodity) ||
            !CanViewCommodity(uid, component, args.Actor, market, commodity))
        {
            return;
        }

        var viewer = EnsureComp<TradingMarketViewerComponent>(args.Actor);
        if (!viewer.ExaminePreviewItems.TryGetValue(args.CommodityId, out var item) || !Exists(item))
        {
            item = Spawn(commodity.Product, doMapInit: false);
            viewer.ExaminePreviewItems[args.CommodityId] = item;
        }

        SendExamineInfo(uid, args.Actor, item, commodity.Product, commodity.Id);
    }

    private bool CanViewCommodity(
        EntityUid pit,
        TradingComponent component,
        EntityUid user,
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity)
    {
        if (HasComp<PublicTradingPitComponent>(pit))
            return GetLowestPublicSellOffer(market.Comp.Offers.Values, commodity.Id) != null;

        return IsTradingPitOwner(user, component) ||
               (commodity.Sections & TradingMarketSection.Unique) != 0;
    }

    private void SendExamineInfo(
        EntityUid pit,
        EntityUid user,
        EntityUid item,
        EntProtoId? previewProduct = null,
        Guid? commodityId = null)
    {
        var message = _examine.GetExamineText(item, user, true);
        var verbs = _verbSystem.GetLocalVerbs(item, user, typeof(ExamineVerb), true);
        _ui.ServerSendUiMessage(
            pit,
            TradingUiKey.Key,
            new TradingExamineInfoMessage(
                GetNetEntity(item),
                message,
                verbs.ToList(),
                previewProduct,
                commodityId),
            user);
    }

    private void OnExecuteExamineVerb(
        EntityUid uid,
        TradingComponent component,
        TradingExecuteExamineVerbMessage args)
    {
        if (!TryGetEntity(args.Item, out var item) ||
            !Exists(item) ||
            !TryComp<TradingMarketViewerComponent>(args.Actor, out var viewer) ||
            !viewer.VisibleItems.Contains(item.Value) &&
            !viewer.ExaminePreviewItems.Values.Contains(item.Value))
        {
            return;
        }

        var verbs = _verbSystem.GetLocalVerbs(item.Value, args.Actor, typeof(ExamineVerb), true);
        if (verbs.TryGetValue(args.RequestedVerb, out var verb))
            _verbSystem.ExecuteVerb(verb, args.Actor, item.Value, true);
    }

    private TradingMarketOfferState CreateOfferState(
        Entity<TradingMarketComponent> market,
        EntityUid store,
        TradingMarketOffer offer,
        bool isOwner)
    {
        var displayName = market.Comp.Commodities.TryGetValue(offer.CommodityId, out var commodity)
            ? commodity.DisplayName
            : offer.Product.Id;
        NetEntity? preview = null;
        if (offer.Item is { } item && Exists(item))
        {
            var metadata = MetaData(item);
            var stackCount = TryComp<StackComponent>(item, out var stack) ? stack.Count : (int?) null;
            displayName = FormatStackName(metadata.EntityName, stackCount);
            preview = GetNetEntity(item);
        }

        return new TradingMarketOfferState(
            offer.Id,
            offer.CommodityId,
            offer.Product,
            offer.Side,
            offer.ParticipantKind,
            offer.ParticipantName,
            offer.Price,
            isOwner && offer.Pit == store,
            displayName,
            preview);
    }

    private void UpdateAllInterfaces(Entity<TradingMarketComponent> market)
    {
        var query = EntityQueryEnumerator<TradingComponent>();
        while (query.MoveNext(out var pit, out var component))
        {
            foreach (var user in _ui.GetActors(pit, TradingUiKey.Key))
            {
                UpdateUserInterface(user, pit, component);
            }
        }
    }

    private void OnRequestUpdate(EntityUid uid, TradingComponent component, TradingRequestUpdateInterfaceMessage args)
    {
        UpdateUserInterface(args.Actor, uid, component);
    }

    private void OnSelectCommodity(EntityUid uid, TradingComponent component, TradingSelectCommodityMessage args)
    {
        var isPublic = HasComp<PublicTradingPitComponent>(uid);
        if (!TryGetMarket(out var market) ||
            !market.Comp.Commodities.TryGetValue(args.CommodityId, out var commodity) ||
            isPublic && GetLowestPublicSellOffer(market.Comp.Offers.Values, commodity.Id) == null ||
            !isPublic && !IsTradingPitOwner(args.Actor, component) &&
            (commodity.Sections & TradingMarketSection.Unique) == 0)
        {
            UpdateUserInterface(args.Actor, uid, component);
            return;
        }

        var viewer = EnsureComp<TradingMarketViewerComponent>(args.Actor);
        viewer.SelectedCommodity = args.CommodityId;
        viewer.SelectedOffer = null;
        UpdateUserInterface(args.Actor, uid, component);
    }

    private void OnSelectOffer(EntityUid uid, TradingComponent component, TradingSelectOfferMessage args)
    {
        if (!TryGetMarket(out var market))
            return;

        var isPublic = HasComp<PublicTradingPitComponent>(uid);
        var isOwner = !isPublic && IsTradingPitOwner(args.Actor, component);
        var viewer = EnsureComp<TradingMarketViewerComponent>(args.Actor);
        if (!market.Comp.Offers.TryGetValue(args.OfferId, out var offer) ||
            !market.Comp.Commodities.TryGetValue(offer.CommodityId, out var commodity) ||
            isPublic && (offer.Side != TradingOfferSide.Sell ||
                         offer.ParticipantKind != TradingParticipantKind.Trader) ||
            !isOwner && !isPublic && (commodity.Sections & TradingMarketSection.Unique) == 0 ||
            !CanSelectOffer(offer, viewer.SelectedCommodity))
        {
            viewer.SelectedOffer = null;
            UpdateUserInterface(args.Actor, uid, component);
            return;
        }

        viewer.SelectedOffer = offer.Id;
        UpdateUserInterface(args.Actor, uid, component);
    }

    private bool CanSelectOffer(
        TradingMarketOffer offer,
        Guid? selectedCommodity)
    {
        if (offer.CommodityId != selectedCommodity)
            return false;

        if (offer.Side == TradingOfferSide.Buy ||
            offer.ParticipantKind == TradingParticipantKind.Guild)
            return true;

        return offer.Item is { } item &&
               Exists(item);
    }

    private void OnUiOpened(EntityUid uid, TradingComponent component, BoundUIOpenedEvent args)
    {
        OpenPublicTradingSession(uid, args.Actor);
        UpdateUserInterface(args.Actor, uid, component);
    }

    private void OnUiClosed(EntityUid uid, TradingComponent component, BoundUIClosedEvent args)
    {
        ClearVisibleMarketItems(args.Actor);
        RemComp<TradingUnitSellRequestComponent>(args.Actor);
        ClosePublicTradingSession(uid, args.Actor);
    }

    private void OnBuyRequest(EntityUid uid, TradingComponent component, TradingBuyMessage msg)
    {
        if (HasComp<PublicTradingPitComponent>(uid))
        {
            BuyPublicCommodity(uid, component, msg.Actor, msg.CommodityId);
            return;
        }

        if (!IsTradingPitOwner(msg.Actor, component) || !TryGetMarket(out var market))
            return;

        var ask = GetLowestSellOffer(market.Comp.Offers.Values, msg.CommodityId, uid);
        if (ask == null || component.Balance < ask.Price)
            return;

        if (!market.Comp.Commodities.TryGetValue(msg.CommodityId, out var commodity) ||
            !CreateTraderBuyOffer(
                market,
                (uid, component),
                MetaData(msg.Actor).EntityName,
                commodity,
                ask.Price,
                msg.Actor,
                out var bid))
        {
            return;
        }

        CompleteTrade(market, commodity, ask, bid, _prototypeManager.Index(market.Comp.Config));
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-purchase-success");
        UpdateAllInterfaces(market);
    }

    private void OnSellRequest(EntityUid uid, TradingComponent component, TradingSellMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) || !TryGetMarket(out var market))
            return;

        var bid = market.Comp.Offers.Values
            .Where(offer => offer.CommodityId == msg.CommodityId &&
                            offer.Side == TradingOfferSide.Buy &&
                            offer.ParticipantKind == TradingParticipantKind.Trader &&
                            offer.Pit != uid)
            .OrderByDescending(offer => offer.Price)
            .ThenBy(offer => offer.Sequence)
            .FirstOrDefault();
        if (bid == null ||
            !market.Comp.Commodities.TryGetValue(msg.CommodityId, out var commodity) ||
            (commodity.Sections & TradingMarketSection.Unique) == 0 ||
            !TryFindInventoryItem(msg.Actor, market, commodity, out var item))
        {
            return;
        }

        if (!TryCreateTraderSellOffer(
                market,
                (uid, component),
                MetaData(msg.Actor).EntityName,
                item,
                msg.Actor,
                bid.Price,
                out var commodityId,
                true) ||
            commodityId != msg.CommodityId)
        {
            return;
        }

        MatchCommodity(market, commodity, _prototypeManager.Index(market.Comp.Config));
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-sale-success");
        UpdateAllInterfaces(market);
    }

    private void OnBuyOfferRequest(EntityUid uid, TradingComponent component, TradingBuyOfferMessage msg)
    {
        if (HasComp<PublicTradingPitComponent>(uid))
        {
            BuyPublicOffer(uid, component, msg.Actor, msg.OfferId);
            return;
        }

        if (!IsTradingPitOwner(msg.Actor, component) ||
            !TryGetMarket(out var market) ||
            !market.Comp.Offers.TryGetValue(msg.OfferId, out var ask) ||
            ask.Side != TradingOfferSide.Sell ||
            ask.Pit == uid ||
            component.Balance < ask.Price ||
            !market.Comp.Commodities.TryGetValue(ask.CommodityId, out var commodity) ||
            !CreateTraderBuyOffer(
                market,
                (uid, component),
                MetaData(msg.Actor).EntityName,
                commodity,
                ask.Price,
                msg.Actor,
                out var bid))
        {
            return;
        }

        CompleteTrade(market, commodity, ask, bid, _prototypeManager.Index(market.Comp.Config));
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-purchase-success");
        UpdateAllInterfaces(market);
    }

    private void OnSellOfferRequest(EntityUid uid, TradingComponent component, TradingSellOfferMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) ||
            !TryGetMarket(out var market) ||
            !market.Comp.Offers.TryGetValue(msg.OfferId, out var bid) ||
            bid.Side != TradingOfferSide.Buy ||
            bid.ParticipantKind != TradingParticipantKind.Trader ||
            bid.Pit == uid ||
            !market.Comp.Commodities.TryGetValue(bid.CommodityId, out var commodity) ||
            !_hands.TryGetActiveItem(msg.Actor, out var held) ||
            held is not { } item)
        {
            return;
        }

        var requiredStackCount = commodity.HasStack ? commodity.BaselineStackCount : (int?) null;
        StackComponent? heldStack = null;
        if (requiredStackCount is { } required &&
            (!TryComp(item, out heldStack) || heldStack.Count < required))
        {
            return;
        }

        if (!TryResolveCommodityForItem(
                market,
                item,
                bid.Price,
                false,
                out var heldCommodity,
                requiredStackCount) ||
            heldCommodity.Id != commodity.Id)
        {
            return;
        }

        var tradeItem = item;
        EntityUid? splitItem = null;
        var originalStackCount = heldStack?.Count;
        if (requiredStackCount is { } splitCount && heldStack != null && heldStack.Count > splitCount)
        {
            splitItem = _stack.Split(item, splitCount, Transform(uid).Coordinates, heldStack);
            if (splitItem == null ||
                !TryResolveCommodityForItem(market, splitItem.Value, bid.Price, false, out var splitCommodity) ||
                splitCommodity.Id != commodity.Id)
            {
                RestoreSplitStack(item, heldStack, originalStackCount, splitItem);
                return;
            }

            tradeItem = splitItem.Value;
        }

        if (!TryCreateTraderSellOffer(
                market,
                (uid, component),
                MetaData(msg.Actor).EntityName,
                tradeItem,
                msg.Actor,
                bid.Price,
                out var commodityId,
                out var ask,
                true))
        {
            RestoreSplitStack(item, heldStack, originalStackCount, splitItem);
            return;
        }

        if (commodityId != bid.CommodityId)
        {
            RemoveOffer(
                market,
                ask.Id,
                true,
                msg.Actor);
            RestoreSplitStack(item, heldStack, originalStackCount, splitItem);
            return;
        }

        CompleteTrade(market, commodity, ask, bid, _prototypeManager.Index(market.Comp.Config));
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-sale-success");
        UpdateAllInterfaces(market);
    }

    private void RestoreSplitStack(
        EntityUid originalItem,
        StackComponent? originalStack,
        int? originalCount,
        EntityUid? splitItem)
    {
        if (originalStack != null && originalCount != null && Exists(originalItem))
            _stack.SetCount(originalItem, originalCount.Value, originalStack);

        if (splitItem is { } split && Exists(split))
            QueueDel(split);
    }

    private void OnCreateSellOffer(EntityUid uid, TradingComponent component, TradingCreateSellOfferMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) || !TryGetMarket(out var market))
            return;

        if (!_hands.TryGetActiveItem(msg.Actor, out var held) ||
            held is not { } item)
        {
            ShowInvalidSellOffer(msg.Actor);
            return;
        }

        CreateSellOffer(market, (uid, component), msg.Actor, item, msg.Price);
    }

    private void OnPrepareUnitSellOffer(
        EntityUid uid,
        TradingComponent component,
        TradingPrepareUnitSellOfferMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) || !TryGetMarket(out var market))
            return;

        if (msg.Price < 0 ||
            !_hands.TryGetActiveItem(msg.Actor, out var held) ||
            held is not { } item ||
            !TryGetUnitSellCandidate(market, item, msg.Price, out var heldCandidate))
        {
            ShowInvalidSellOffer(msg.Actor);
            return;
        }

        var candidates = new List<TradingUnitSellCandidate>
        {
            heldCandidate,
        };

        foreach (var candidate in GetTraderInventoryItems(msg.Actor))
        {
            if (candidate == item ||
                !TryGetUnitSellCandidate(market, candidate, msg.Price, out var unitCandidate) ||
                !string.Equals(unitCandidate.Signature, heldCandidate.Signature, StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add(unitCandidate);
        }

        var maximumAmount = (int) Math.Min(int.MaxValue, candidates.Sum(candidate => (long) candidate.Amount));
        if (maximumAmount == 1)
        {
            RemComp<TradingUnitSellRequestComponent>(msg.Actor);
            CreateSellOffer(market, (uid, component), msg.Actor, item, msg.Price);
            return;
        }

        var request = EnsureComp<TradingUnitSellRequestComponent>(msg.Actor);
        request.RequestId = Guid.NewGuid();
        request.Pit = uid;
        request.Price = msg.Price;
        request.Candidates = candidates;

        _ui.ServerSendUiMessage(
            uid,
            TradingUiKey.Key,
            new TradingUnitSellOfferPreparedMessage(
                request.RequestId,
                MetaData(item).EntityName,
                request.Price,
                maximumAmount),
            msg.Actor);
    }

    private void OnCreateUnitSellOffers(
        EntityUid uid,
        TradingComponent component,
        TradingCreateUnitSellOffersMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) ||
            !TryGetMarket(out var market) ||
            !TryComp<TradingUnitSellRequestComponent>(msg.Actor, out var request) ||
            request.Pit != uid ||
            request.RequestId != msg.RequestId)
        {
            return;
        }

        var price = request.Price;
        var remaining = msg.Amount;
        var candidates = new List<(TradingUnitSellCandidate Candidate, int Amount)>();
        if (remaining > 0)
        {
            foreach (var candidate in request.Candidates)
            {
                var amount = Math.Min(candidate.Amount, remaining);
                if (amount > 0)
                    candidates.Add((candidate, amount));

                remaining -= amount;
                if (remaining == 0)
                    break;
            }
        }

        var amountIsValid = msg.Amount > 0 && remaining == 0;
        RemComp<TradingUnitSellRequestComponent>(msg.Actor);

        if (!amountIsValid)
        {
            ShowInvalidatedUnitSellOffer(msg.Actor);
            return;
        }

        var inventoryItems = GetTraderInventoryItems(msg.Actor).ToHashSet();
        foreach (var (candidate, amount) in candidates)
        {
            if (!inventoryItems.Contains(candidate.Item) ||
                GetUnitSellAmount(candidate.Item) < amount ||
                !TryGetTradingItemSignature(
                    market,
                    candidate.Item,
                    price,
                    out var signature,
                    HasComp<StackComponent>(candidate.Item) ? 1 : null) ||
                !string.Equals(signature, candidate.Signature, StringComparison.Ordinal))
            {
                ShowInvalidatedUnitSellOffer(msg.Actor);
                return;
            }
        }

        var participantName = MetaData(msg.Actor).EntityName;
        var createdOffers = new List<Guid>();
        var commodityIds = new HashSet<Guid>();
        foreach (var (candidate, amount) in candidates)
        {
            for (var index = 0; index < amount; index++)
            {
                var tradeItem = candidate.Item;
                var wasSplit = false;
                if (TryComp<StackComponent>(candidate.Item, out var stack) && stack.Count > 1)
                {
                    var splitItem = _stack.Split(candidate.Item, 1, Transform(uid).Coordinates, stack);
                    if (splitItem == null)
                    {
                        RollbackUnitSellOffers(market, createdOffers, msg.Actor);
                        ShowInvalidatedUnitSellOffer(msg.Actor);
                        UpdateAllInterfaces(market);
                        return;
                    }

                    tradeItem = splitItem.Value;
                    wasSplit = true;
                }

                if (!TryCreateTraderSellOffer(
                        market,
                        (uid, component),
                        participantName,
                        tradeItem,
                        msg.Actor,
                        price,
                        out var commodityId,
                        out var offer))
                {
                    if (wasSplit)
                        _delivery.Deliver(tradeItem, msg.Actor);

                    RollbackUnitSellOffers(market, createdOffers, msg.Actor);
                    ShowInvalidatedUnitSellOffer(msg.Actor);
                    UpdateAllInterfaces(market);
                    return;
                }

                createdOffers.Add(offer.Id);
                commodityIds.Add(commodityId);
            }
        }

        var config = _prototypeManager.Index(market.Comp.Config);
        foreach (var commodityId in commodityIds)
        {
            if (market.Comp.Commodities.TryGetValue(commodityId, out var commodity))
                MatchCommodity(market, commodity, config);
        }

        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-unit-sell-offers-created");
        UpdateAllInterfaces(market);
    }

    private void RollbackUnitSellOffers(
        Entity<TradingMarketComponent> market,
        List<Guid> createdOffers,
        EntityUid actor)
    {
        foreach (var offerId in createdOffers)
        {
            RemoveOffer(market, offerId, true, actor);
        }
    }

    private bool TryGetUnitSellCandidate(
        Entity<TradingMarketComponent> market,
        EntityUid item,
        int price,
        out TradingUnitSellCandidate candidate)
    {
        candidate = default!;
        var amount = GetUnitSellAmount(item);
        if (amount <= 0 ||
            !TryGetTradingItemSignature(
                market,
                item,
                price,
                out var signature,
                HasComp<StackComponent>(item) ? 1 : null))
        {
            return false;
        }

        candidate = new TradingUnitSellCandidate
        {
            Item = item,
            Signature = signature,
            Amount = amount,
        };
        return true;
    }

    private int GetUnitSellAmount(EntityUid item)
    {
        return TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
    }

    private bool CreateSellOffer(
        Entity<TradingMarketComponent> market,
        Entity<TradingComponent> pit,
        EntityUid actor,
        EntityUid item,
        int price)
    {
        if (!TryCreateTraderSellOffer(
                market,
                pit,
                MetaData(actor).EntityName,
                item,
                actor,
                price,
                out var commodityId))
        {
            ShowInvalidSellOffer(actor);
            return false;
        }

        if (!market.Comp.Commodities.TryGetValue(commodityId, out var commodity))
            return false;

        MatchCommodity(market, commodity, _prototypeManager.Index(market.Comp.Config));
        ShowTradingSuccess(actor, pit.Owner, pit.Comp, "trading-ui-sell-offer-created");
        UpdateAllInterfaces(market);
        return true;
    }

    private void ShowInvalidSellOffer(EntityUid actor)
    {
        _popup.PopupCursor(
            Loc.GetString("trading-ui-invalid-sell-offer"),
            actor,
            PopupType.SmallCaution);
    }

    private void ShowInvalidatedUnitSellOffer(EntityUid actor)
    {
        _popup.PopupCursor(
            Loc.GetString("trading-ui-unit-sell-offer-invalidated"),
            actor,
            PopupType.SmallCaution);
    }

    private void OnCreateBuyOffer(EntityUid uid, TradingComponent component, TradingCreateBuyOfferMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) ||
            !TryGetMarket(out var market) ||
            !market.Comp.Commodities.TryGetValue(msg.CommodityId, out var commodity))
        {
            return;
        }

        if (component.Balance < msg.Price)
        {
            ShowInsufficientOrderFunds(msg.Actor);
            return;
        }

        if (!CreateTraderBuyOffer(
                market,
                (uid, component),
                MetaData(msg.Actor).EntityName,
                commodity,
                msg.Price))
        {
            return;
        }

        MatchCommodity(market, commodity, _prototypeManager.Index(market.Comp.Config));
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-buy-order-created");
        UpdateAllInterfaces(market);
    }

    private void OnCreateBuyOfferFromHeld(
        EntityUid uid,
        TradingComponent component,
        TradingCreateBuyOfferFromHeldMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) || !TryGetMarket(out var market))
            return;

        var config = _prototypeManager.Index(market.Comp.Config);
        if (!_hands.TryGetActiveItem(msg.Actor, out var held) ||
            held is not { } item ||
            msg.Price <= 0)
        {
            return;
        }

        if (component.Balance < msg.Price)
        {
            ShowInsufficientOrderFunds(msg.Actor);
            return;
        }

        if (!TryResolveCommodityForItem(
                market,
                item,
                msg.Price,
                true,
                out var commodity,
                forceIntactEquipment: true) ||
            !CreateTraderBuyOffer(
                market,
                (uid, component),
                MetaData(msg.Actor).EntityName,
                commodity,
                msg.Price))
        {
            return;
        }

        MatchCommodity(market, commodity, config);
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-buy-order-created");
        UpdateAllInterfaces(market);
    }

    private void OnCancelOffer(EntityUid uid, TradingComponent component, TradingCancelOfferMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) ||
            !TryGetMarket(out var market) ||
            !market.Comp.Offers.TryGetValue(msg.OfferId, out var offer) ||
            offer.Pit != uid)
        {
            return;
        }

        RemoveOffer(
            market,
            msg.OfferId,
            true,
            msg.Actor);
        UpdateAllInterfaces(market);
    }

    private void OnCollectStoredItem(EntityUid uid, TradingComponent component, TradingCollectStoredItemMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component))
            return;

        var item = GetEntity(msg.Item);
        if (!Exists(item) || !component.StoredMarketItems.Contains(item))
            return;

        DeliverItem(uid, component, item, msg.Actor);
        UpdateUserInterface(msg.Actor, uid, component);
    }

    private void OnCollectSaleRevenue(
        EntityUid uid,
        TradingComponent component,
        TradingCollectSaleRevenueMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component))
            return;

        var index = component.PendingSales.FindIndex(sale => sale.Id == msg.SaleId);
        if (index < 0)
            return;

        var sale = component.PendingSales[index];
        component.PendingSales.RemoveAt(index);
        component.Balance += sale.Price;
        component.MarketArchive.Add(
            Loc.GetString(
                "trading-ui-archive-sell-entry",
                ("item", sale.ItemName),
                ("trader", sale.BuyerName),
                ("price", sale.Price)));
        ShowTradingSuccess(msg.Actor, uid, component, "trading-ui-sale-revenue-collected");
        UpdateUserInterface(msg.Actor, uid, component);
    }

    internal bool CreateTraderBuyOffer(
        Entity<TradingMarketComponent> market,
        Entity<TradingComponent> pit,
        string participantName,
        TradingCommodity commodity,
        int price,
        EntityUid? immediateRecipient = null)
    {
        return CreateTraderBuyOffer(
            market,
            pit,
            participantName,
            commodity,
            price,
            immediateRecipient,
            out _);
    }

    private bool CreateTraderBuyOffer(
        Entity<TradingMarketComponent> market,
        Entity<TradingComponent> pit,
        string participantName,
        TradingCommodity commodity,
        int price,
        EntityUid? immediateRecipient,
        out TradingMarketOffer offer)
    {
        offer = default!;
        var config = _prototypeManager.Index(market.Comp.Config);
        if (price < 0 ||
            (price == 0 && immediateRecipient == null) ||
            !CanTradeProduct(commodity.Product, config) ||
            pit.Comp.Balance < price ||
            !market.Comp.Commodities.ContainsKey(commodity.Id))
        {
            return false;
        }

        pit.Comp.Balance -= price;
        offer = new TradingMarketOffer
        {
            Id = Guid.NewGuid(),
            CommodityId = commodity.Id,
            Product = commodity.Product,
            Side = TradingOfferSide.Buy,
            ParticipantKind = TradingParticipantKind.Trader,
            ParticipantName = participantName,
            Price = price,
            Pit = pit.Owner,
            ImmediateRecipient = immediateRecipient,
            IsImmediate = immediateRecipient != null,
            Sequence = market.Comp.NextSequence++,
        };
        AddOffer(market, offer);
        return true;
    }

    internal bool TryCreateTraderSellOffer(
        Entity<TradingMarketComponent> market,
        Entity<TradingComponent> pit,
        string participantName,
        EntityUid sourceItem,
        EntityUid seller,
        int price,
        out Guid commodityId,
        bool immediate = false)
    {
        return TryCreateTraderSellOffer(
            market,
            pit,
            participantName,
            sourceItem,
            seller,
            price,
            out commodityId,
            out _,
            immediate);
    }

    private bool TryCreateTraderSellOffer(
        Entity<TradingMarketComponent> market,
        Entity<TradingComponent> pit,
        string participantName,
        EntityUid sourceItem,
        EntityUid seller,
        int price,
        out Guid commodityId,
        out TradingMarketOffer offer,
        bool immediate = false)
    {
        commodityId = default;
        offer = default!;
        if (price < 0 ||
            !TryResolveCommodityForItem(market, sourceItem, price, true, out var commodity) ||
            MetaData(sourceItem).EntityPrototype?.ID is not { } product)
        {
            return false;
        }

        var destination = _containers.EnsureContainer<Container>(pit.Owner, TradingComponent.MarketContainerId);
        BaseContainer? previousContainer = null;
        if (_containers.TryGetContainingContainer((sourceItem, null, null), out previousContainer) &&
            !_containers.Remove(sourceItem, previousContainer, reparent: false, force: true))
        {
            return false;
        }

        EmptyItemStorage(sourceItem, seller);

        if (!_containers.Insert(sourceItem, destination, force: true))
        {
            if (previousContainer != null)
                _containers.Insert(sourceItem, previousContainer, force: true);
            TryRemoveCommodity(market, commodity);
            return false;
        }

        offer = new TradingMarketOffer
        {
            Id = Guid.NewGuid(),
            CommodityId = commodity.Id,
            Product = product,
            Side = TradingOfferSide.Sell,
            ParticipantKind = TradingParticipantKind.Trader,
            ParticipantName = participantName,
            Price = price,
            Pit = pit.Owner,
            Item = sourceItem,
            ListedItemName = MetaData(sourceItem).EntityName,
            IsImmediate = immediate,
            Sequence = market.Comp.NextSequence++,
        };
        AddOffer(market, offer);
        commodityId = commodity.Id;
        return true;
    }

    private void EmptyItemStorage(EntityUid item, EntityUid seller)
    {
        if (!TryComp<StorageComponent>(item, out var storage))
            return;

        var coordinates = Transform(seller).Coordinates;
        _containers.EmptyContainer(storage.Container, true, coordinates);
    }

    private bool TryFindInventoryItem(
        EntityUid user,
        Entity<TradingMarketComponent> market,
        TradingCommodity selected,
        out EntityUid item)
    {
        foreach (var candidate in GetTraderInventoryItems(user))
        {
            if (TryResolveCommodityForItem(market, candidate, selected.StandardPrice, false, out var commodity) &&
                commodity.Id == selected.Id)
            {
                item = candidate;
                return true;
            }
        }

        item = default;
        return false;
    }

    private bool TryGetTradingItemSignature(
        Entity<TradingMarketComponent> market,
        EntityUid item,
        int price,
        out string signature,
        int? stackCountOverride = null)
    {
        signature = string.Empty;
        if (!TryResolveCommodityForItem(
                market,
                item,
                price,
                true,
                out var commodity,
                stackCountOverride))
        {
            return false;
        }

        signature = commodity.Signature;
        TryRemoveCommodity(market, commodity);
        return true;
    }

    private List<EntityUid> GetTraderInventoryItems(EntityUid user)
    {
        var items = new List<EntityUid>();
        var pending = new Queue<EntityUid>(_inventory.GetHandOrInventoryEntities(user));
        var visited = new HashSet<EntityUid>();
        while (pending.TryDequeue(out var candidate))
        {
            if (!visited.Add(candidate))
                continue;

            items.Add(candidate);

            if (TryComp<StorageComponent>(candidate, out var storage))
            {
                foreach (var contained in storage.Container.ContainedEntities)
                {
                    pending.Enqueue(contained);
                }
            }
        }

        return items;
    }

    private void DeliverItem(
        EntityUid pitUid,
        TradingComponent pit,
        EntityUid item,
        EntityUid? recipient)
    {
        if (!Exists(item) || TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item))
        {
            pit.StoredMarketItems.Remove(item);
            return;
        }

        if (recipient is { } user && Exists(user))
        {
            pit.StoredMarketItems.Remove(item);
            _delivery.Deliver(item, user);
            return;
        }

        StoreItemInPit(pitUid, pit, item);
    }

    private bool StoreItemInPit(EntityUid pitUid, TradingComponent pit, EntityUid item)
    {
        var destination = _containers.EnsureContainer<Container>(pitUid, TradingComponent.MarketContainerId);
        BaseContainer? previousContainer = null;
        if (_containers.TryGetContainingContainer((item, null, null), out previousContainer) &&
            (previousContainer.Owner != pitUid || previousContainer.ID != TradingComponent.MarketContainerId))
        {
            if (!_containers.Remove(item, previousContainer, reparent: false, force: true))
                return false;

            if (!_containers.Insert(item, destination, force: true))
            {
                _containers.Insert(item, previousContainer, force: true);
                return false;
            }
        }
        else if (previousContainer == null && !_containers.Insert(item, destination, force: true))
        {
            return false;
        }

        if (!pit.StoredMarketItems.Contains(item))
            pit.StoredMarketItems.Add(item);
        return true;
    }

    private void RefreshVisibleMarketItems(
        EntityUid user,
        EntityUid store,
        TradingComponent component,
        Entity<TradingMarketComponent> market,
        bool isOwner,
        bool isPublic)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var viewer = EnsureComp<TradingMarketViewerComponent>(user);
        var desired = market.Comp.Commodities.Values
            .Where(commodity => isPublic
                ? GetLowestPublicSellOffer(market.Comp.Offers.Values, commodity.Id) != null
                : isOwner || (commodity.Sections & TradingMarketSection.Unique) != 0)
            .Select(commodity => (isPublic
                ? GetLowestPublicSellOffer(market.Comp.Offers.Values, commodity.Id)
                : GetLowestSellOffer(
                    market.Comp.Offers.Values,
                    commodity.Id,
                    isOwner ? store : null))?.Item)
            .Where(item => item != null && Exists(item.Value))
            .Select(item => item!.Value)
            .ToHashSet();

        if (isOwner)
        {
            desired.UnionWith(market.Comp.Offers.Values
                .Where(offer => offer.Pit == store && offer.Item is { } item && Exists(item))
                .Select(offer => offer.Item!.Value));
        }

        if (viewer.SelectedOffer is { } selectedOffer &&
            market.Comp.Offers.TryGetValue(selectedOffer, out var offer) &&
            CanSelectOffer(offer, viewer.SelectedCommodity))
        {
            if (offer.Item is { } selectedItem && Exists(selectedItem))
                desired.Add(selectedItem);
        }
        else
        {
            viewer.SelectedOffer = null;
        }

        if (isOwner)
            desired.UnionWith(component.StoredMarketItems.Where(Exists));

        foreach (var item in viewer.VisibleItems.Except(desired).ToList())
        {
            if (Exists(item))
                _pvs.RemoveForceSend(item, actor.PlayerSession);
            viewer.VisibleItems.Remove(item);
        }

        foreach (var item in desired.Except(viewer.VisibleItems))
        {
            _pvs.AddForceSend(item, actor.PlayerSession);
            viewer.VisibleItems.Add(item);
        }
    }

    private void ClearVisibleMarketItems(EntityUid user)
    {
        if (!TryComp<TradingMarketViewerComponent>(user, out var viewer))
            return;

        if (TryComp<ActorComponent>(user, out var actor))
        {
            foreach (var item in viewer.VisibleItems)
            {
                if (Exists(item))
                    _pvs.RemoveForceSend(item, actor.PlayerSession);
            }
        }

        foreach (var item in viewer.ExaminePreviewItems.Values)
        {
            if (Exists(item))
                QueueDel(item);
        }

        RemCompDeferred<TradingMarketViewerComponent>(user);
    }

    internal static TradingMarketOffer? GetLowestSellOffer(
        IEnumerable<TradingMarketOffer> offers,
        Guid commodityId,
        EntityUid? excludedPit = null)
    {
        return offers
            .Where(offer => offer.CommodityId == commodityId &&
                            offer.Side == TradingOfferSide.Sell &&
                            (excludedPit == null || offer.Pit != excludedPit))
            .OrderBy(offer => offer.Price)
            .ThenBy(offer => offer.Sequence)
            .FirstOrDefault();
    }

    private void OnRequestWithdraw(EntityUid uid, TradingComponent component, TradingRequestWithdrawMessage msg)
    {
        if (!IsTradingPitOwner(msg.Actor, component) || msg.Amount <= 0 || component.Balance < msg.Amount)
            return;

        if (!_prototypeManager.TryIndex(component.Currency, out var prototype) ||
            prototype.Cash == null ||
            !prototype.CanWithdraw)
        {
            return;
        }

        FixedPoint2 amountRemaining = msg.Amount;
        var coordinates = Transform(msg.Actor).Coordinates;
        foreach (var value in prototype.Cash.Keys.OrderByDescending(value => value))
        {
            var amountToSpawn = (int) MathF.Floor((float) (amountRemaining / value));
            var entities = _stack.SpawnMultiple(prototype.Cash[value], amountToSpawn, coordinates);
            foreach (var entity in entities)
            {
                _delivery.Deliver(entity, msg.Actor);
            }
            amountRemaining -= value * amountToSpawn;
        }

        component.Balance -= msg.Amount;
        UpdateUserInterface(msg.Actor, uid, component);
    }

    private void ShowTradingSuccess(
        EntityUid actor,
        EntityUid pit,
        TradingComponent component,
        string message)
    {
        _popup.PopupCursor(Loc.GetString(message), actor);
        _audio.PlayEntity(component.BuySuccessSound, actor, pit);
    }

    private void ShowInsufficientOrderFunds(EntityUid actor)
    {
        _popup.PopupCursor(
            Loc.GetString("trading-ui-insufficient-order-funds"),
            actor,
            PopupType.SmallCaution);
    }
}
