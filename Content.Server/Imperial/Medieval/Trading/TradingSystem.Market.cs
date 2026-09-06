using System.Globalization;
using System.Linq;
using Content.Server.Imperial.Medieval.Courier;
using Content.Server.Light.Components;
using Content.Server.MedievalMoneyChecker.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
using Content.Shared.Imperial.Medieval.SmithingSystem;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Trigger.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    internal bool CreateMarket()
    {
        DeleteMarket();
        if (!TryFindMarketMap(out var map))
        {
            Log.Error("Trading market could not be created because no active map exists.");
            return false;
        }

        var uid = Spawn(null, new EntityCoordinates(map, 0f, 0f));
        var market = EnsureComp<TradingMarketComponent>(uid);
        _market = uid;

        var config = _prototypeManager.Index(market.Config);
        foreach (var guildType in _prototypeManager.EnumeratePrototypes<GuildTypePrototype>())
        {
            if (config.GuildTypes.Count > 0 && !config.GuildTypes.Contains(guildType.ID))
                continue;

            for (var index = 0; index < guildType.MaximumGuilds; index++)
            {
                market.Guilds.Add(new Guild(guildType, _random, _prototypeManager));
            }

            foreach (var item in guildType.Items)
            {
                if (item.ProductEntity is not { } product)
                    continue;

                var prototype = _prototypeManager.Index(product);
                if (!CanTradeProduct(prototype, config))
                    continue;

                TradingCommodity commodity;
                if (!market.CommonCommodities.TryGetValue(product, out var commodityId))
                {
                    prototype.TryGetComponent<StackComponent>(out var stack, EntityManager.ComponentFactory);
                    commodityId = Guid.NewGuid();
                    commodity = new TradingCommodity
                    {
                        Id = commodityId,
                        Product = product,
                        Sections = TradingMarketSection.Common,
                        StandardPrice = Math.Max(1, item.Cost),
                        BaselineStackCount = stack?.Count ?? 1,
                        HasStack = stack != null,
                        Permanent = true,
                        Signature = $"common:{product.Id}",
                        DisplayName = FormatStackName(prototype.Name, stack?.Count),
                        Description = prototype.Description,
                    };
                    market.Commodities.Add(commodityId, commodity);
                    market.CommonCommodities.Add(product, commodityId);
                }
                else
                {
                    commodity = market.Commodities[commodityId];
                }

                commodity.Categories.Add(guildType.ID);
                commodity.MinReputation = Math.Max(commodity.MinReputation, item.MinReputation);
            }
        }

        InitializeReputationScarcity(market, config);
        SeedGuildOffers((uid, market), config);
        return true;
    }

    private bool TryFindMarketMap(out EntityUid map)
    {
        var maps = EntityQueryEnumerator<MapComponent>();
        while (maps.MoveNext(out var mapUid, out _))
        {
            if (TerminatingOrDeleted(mapUid) || EntityManager.IsQueuedForDeletion(mapUid))
                continue;

            map = mapUid;
            return true;
        }

        map = default;
        return false;
    }

    private void DeleteMarket()
    {
        var query = EntityQueryEnumerator<TradingMarketComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid))
                QueueDel(uid);
        }

        _market = null;
    }

    private void RunMarketStep(
        Entity<TradingMarketComponent> market,
        TradingMarketConfigPrototype config)
    {
        RefreshMarketOrderBooks(market.Comp);
        MatchAll(market, config);
        CreateGuildInterventions(market, config);
        MatchAll(market, config);
        RemoveUncompetitiveGuildOffers(market, config);
        AdvanceReputationScarcity(market);
    }

    private void SeedGuildOffers(
        Entity<TradingMarketComponent> market,
        TradingMarketConfigPrototype config)
    {
        foreach (var commodityId in market.Comp.CommonCommodities.Values)
        {
            if (!market.Comp.Commodities.TryGetValue(commodityId, out var commodity))
                continue;

            var candidates = GetGuildCandidates(market, commodity);
            if (candidates.Count == 0)
                continue;

            var referencePrice = GetGuildReferencePrice(commodity);
            var sellCount = Math.Min(GetGuildOfferTarget(commodity, config), config.MaximumGuildSellOfferCount);
            for (var index = 0; index < sellCount; index++)
            {
                var depth = GetInitialGuildOfferDepth(index, sellCount, config.InitialGuildPriceDepth);
                var price = RoundInitialGuildOfferPrice(
                    GetInitialGuildOfferPrice(
                        referencePrice,
                        TradingOfferSide.Sell,
                        config.InitialGuildPriceSpread,
                        depth),
                    TradingOfferSide.Sell);
                CreateGuildOffer(
                    market,
                    _random.Pick(candidates),
                    commodity,
                    TradingOfferSide.Sell,
                    price);
            }

            var buyCount = Math.Min(GetGuildOfferTarget(commodity, config), config.MaximumGuildBuyOrderCount);
            for (var index = 0; index < buyCount; index++)
            {
                var depth = GetInitialGuildOfferDepth(index, buyCount, config.InitialGuildPriceDepth);
                var price = RoundInitialGuildOfferPrice(
                    GetInitialGuildOfferPrice(
                        referencePrice,
                        TradingOfferSide.Buy,
                        config.InitialGuildPriceSpread,
                        depth),
                    TradingOfferSide.Buy);
                CreateGuildOffer(
                    market,
                    _random.Pick(candidates),
                    commodity,
                    TradingOfferSide.Buy,
                    price);
            }
        }
    }

    private List<Guild> GetGuildCandidates(
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity)
    {
        return market.Comp.Guilds
            .Where(guild => guild.Items.Any(item => item.ProductEntity is { } product &&
                                                   product == commodity.Product))
            .ToList();
    }

    internal static float GetExpectedGuildOfferCount(
        TradingCommodity commodity,
        TradingMarketConfigPrototype config)
    {
        var price = GetGuildReferencePrice(commodity);
        var referencePrice = Math.Max(1, config.LiquidityReferencePrice);
        var expected = config.LiquidityReferenceOfferCount * (float) referencePrice / price;
        return Math.Clamp(expected, config.MinimumGuildOfferCount, config.MaximumGuildOfferCount);
    }

    internal static int GetGuildOfferTarget(
        TradingCommodity commodity,
        TradingMarketConfigPrototype config)
    {
        return (int) MathF.Round(GetExpectedGuildOfferCount(commodity, config));
    }

    private void CreateGuildOffer(
        Entity<TradingMarketComponent> market,
        Guild guild,
        TradingCommodity commodity,
        TradingOfferSide side,
        int price)
    {
        AddOffer(market, new TradingMarketOffer
        {
            Id = Guid.NewGuid(),
            CommodityId = commodity.Id,
            Product = commodity.Product,
            Side = side,
            ParticipantKind = TradingParticipantKind.Guild,
            ParticipantName = guild.Name,
            Price = price,
            GuildId = guild.Id,
            Sequence = market.Comp.NextSequence++,
        });
    }

    private static int RoundMarketPrice(float price)
    {
        if (float.IsNaN(price) || price <= 1f)
            return 1;

        return price >= int.MaxValue ? int.MaxValue : (int) MathF.Round(price);
    }

    private void AddOffer(
        Entity<TradingMarketComponent> market,
        TradingMarketOffer offer)
    {
        market.Comp.Offers.Add(offer.Id, offer);
        AddOfferToBook(
            market.Comp.Commodities[offer.CommodityId],
            offer.Side,
            offer.Price,
            offer.ParticipantKind == TradingParticipantKind.Guild,
            market.Comp.PriceWeightBase);
        if (offer.Pit is { } pit && TryComp<TradingComponent>(pit, out var trading))
            trading.MarketOffers.Add(offer.Id);
    }

    private void MatchAll(
        Entity<TradingMarketComponent> market,
        TradingMarketConfigPrototype config)
    {
        foreach (var commodity in market.Comp.Commodities.Values.ToList())
        {
            MatchCommodity(market, commodity, config);
        }
    }

    internal void MatchCommodity(
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity,
        TradingMarketConfigPrototype config)
    {
        while (true)
        {
            var asks = market.Comp.Offers.Values
                .Where(offer => offer.CommodityId == commodity.Id && offer.Side == TradingOfferSide.Sell)
                .OrderBy(offer => offer.Price)
                .ThenBy(offer => offer.Sequence)
                .ToList();
            var bids = market.Comp.Offers.Values
                .Where(offer => offer.CommodityId == commodity.Id && offer.Side == TradingOfferSide.Buy)
                .OrderByDescending(offer => offer.Price)
                .ThenBy(offer => offer.Sequence)
                .ToList();

            TradingMarketOffer? ask = null;
            TradingMarketOffer? bid = null;
            foreach (var candidateAsk in asks)
            {
                var candidateBid = bids.FirstOrDefault(value => CanMatchOffers(candidateAsk, value));
                if (candidateBid == null)
                    continue;

                ask = candidateAsk;
                bid = candidateBid;
                break;
            }

            if (ask == null || bid == null)
                break;

            CompleteTrade(market, commodity, ask, bid, config);
        }
    }

    internal static bool CanMatchOffers(TradingMarketOffer ask, TradingMarketOffer bid)
    {
        return bid.Price >= ask.Price &&
               !IsSameParticipant(ask, bid) &&
               !(ask.ParticipantKind == TradingParticipantKind.Trader &&
                 bid.ParticipantKind == TradingParticipantKind.Guild);
    }

    private static bool IsSameParticipant(TradingMarketOffer first, TradingMarketOffer second)
    {
        if (first.ParticipantKind != second.ParticipantKind)
            return false;

        return first.ParticipantKind == TradingParticipantKind.Trader
            ? first.Pit == second.Pit
            : first.GuildId == second.GuildId;
    }

    private void CompleteTrade(
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity,
        TradingMarketOffer ask,
        TradingMarketOffer bid,
        TradingMarketConfigPrototype config)
    {
        if (ask.Item is { } escrowItem)
        {
            if (!CanTradeItem(escrowItem, config))
            {
                Log.Error(
                    $"Trading market could not complete trade for sell offer {ask.Id}: escrow item {escrowItem} " +
                    $"for product {ask.Product} is no longer eligible for trading.");
                RemoveOffer(market, ask.Id, true);
                if (bid.IsImmediate)
                    RemoveOffer(market, bid.Id, false);
                return;
            }

            if (!CanTransferEscrowItem(ask, escrowItem))
            {
                Log.Error(
                    $"Trading market could not complete trade for sell offer {ask.Id}: escrow item {escrowItem} " +
                    $"for product {ask.Product} no longer exists or is no longer held by trading pit {ask.Pit}.");
                RemoveOffer(market, ask.Id, false);
                if (bid.IsImmediate)
                    RemoveOffer(market, bid.Id, false);
                return;
            }

            var currentName = MetaData(escrowItem).EntityName;
            if (!string.Equals(currentName, ask.ListedItemName, StringComparison.Ordinal))
            {
                Log.Error(
                    $"Trading market sell offer {ask.Id} changed item name while in escrow: " +
                    $"'{ask.ListedItemName}' -> '{currentName}' for item {escrowItem} and product {ask.Product}.");
            }
        }

        var executionPrice = ask.Sequence < bid.Sequence ? ask.Price : bid.Price;
        var sellerPayoutDeferred = ArchiveTrade(commodity, ask, bid, executionPrice);

        if (!bid.UsesExternalFunds &&
            bid.Pit is { } buyerPit &&
            TryComp<TradingComponent>(buyerPit, out var buyer))
        {
            buyer.Balance += bid.Price - executionPrice;
        }

        if (!sellerPayoutDeferred &&
            ask.Pit is { } sellerPit &&
            TryComp<TradingComponent>(sellerPit, out var seller))
        {
            seller.Balance += executionPrice;
        }

        if (ask.Item is { } item)
        {
            if (bid.Pit is { } destination && TryComp<TradingComponent>(destination, out var destinationPit))
                DeliverItem(destination, destinationPit, item, bid.ImmediateRecipient);
            else
                QueueDel(item);
        }
        else if (ask.ParticipantKind == TradingParticipantKind.Guild &&
                 bid.Pit is { } destination &&
                 TryComp<TradingComponent>(destination, out var destinationPit))
        {
            var productPrototype = _prototypeManager.Index(ask.Product);
            var spawnCoordinates = MapCoordinates.Nullspace;
            if (!productPrototype.HasComponent<ItemComponent>())
            {
                var spawnTarget = bid.ImmediateRecipient is { } recipient && Exists(recipient)
                    ? recipient
                    : destination;
                spawnCoordinates = Transform(spawnTarget).MapPosition;
            }

            var spawnedItem = Spawn(ask.Product, spawnCoordinates);
            EnsureComp<TradingLotBlockedComponent>(spawnedItem);
            DeliverItem(destination, destinationPit, spawnedItem, bid.ImmediateRecipient);
        }

        RemoveOfferRecord(market, ask);
        RemoveOfferRecord(market, bid);
        TryRemoveCommodity(market, commodity);
    }

    private bool CanTransferEscrowItem(TradingMarketOffer offer, EntityUid item)
    {
        if (!Exists(item) ||
            TerminatingOrDeleted(item) ||
            EntityManager.IsQueuedForDeletion(item) ||
            offer.Pit is not { } sellerPit ||
            !TryComp<TradingComponent>(sellerPit, out _) ||
            !_containers.TryGetContainingContainer((item, null, null), out var container))
        {
            return false;
        }

        return container.Owner == sellerPit && container.ID == TradingComponent.MarketContainerId;
    }

    private bool ArchiveTrade(
        TradingCommodity commodity,
        TradingMarketOffer ask,
        TradingMarketOffer bid,
        int executionPrice)
    {
        var displayName = commodity.DisplayName;
        var sellerPayoutDeferred = false;
        if (ask.Item is { } item && Exists(item))
        {
            var metadata = MetaData(item);
            var stackCount = TryComp<StackComponent>(item, out var stack) ? stack.Count : (int?) null;
            displayName = FormatStackName(metadata.EntityName, stackCount);
        }

        if (ask.ParticipantKind == TradingParticipantKind.Trader &&
            !ask.IsImmediate &&
            ask.Pit is { } sellerPit &&
            TryComp<TradingComponent>(sellerPit, out var seller))
        {
            seller.PendingSales.Add(new TradingPendingSale
            {
                Id = ask.Id,
                ItemName = displayName,
                BuyerName = bid.ParticipantName,
                Price = executionPrice,
            });
            sellerPayoutDeferred = true;
        }

        if (bid.ParticipantKind == TradingParticipantKind.Trader &&
            !bid.IsImmediate &&
            bid.Pit is { } buyerPit &&
            TryComp<TradingComponent>(buyerPit, out var buyer))
        {
            buyer.MarketArchive.Add(
                Loc.GetString(
                    "trading-ui-archive-buy-entry",
                    ("item", displayName),
                    ("trader", ask.ParticipantName),
                    ("price", executionPrice)));
        }

        return sellerPayoutDeferred;
    }

    internal void RemoveOffer(
        Entity<TradingMarketComponent> market,
        Guid id,
        bool returnEscrow,
        EntityUid? recipient = null)
    {
        if (!market.Comp.Offers.TryGetValue(id, out var offer))
            return;

        if (offer.Side == TradingOfferSide.Buy && !offer.UsesExternalFunds)
        {
            if (offer.Pit is { } buyerId && TryComp<TradingComponent>(buyerId, out var buyer))
                buyer.Balance += offer.Price;
        }

        if (offer.Item is { } item && returnEscrow)
        {
            if (offer.Pit is { } sellerId && TryComp<TradingComponent>(sellerId, out var seller))
                DeliverItem(sellerId, seller, item, recipient);
            else
                QueueDel(item);
        }

        RemoveOfferRecord(market, offer);
        if (market.Comp.Commodities.TryGetValue(offer.CommodityId, out var removedCommodity))
            TryRemoveCommodity(market, removedCommodity);
    }

    private void RemoveOfferRecord(
        Entity<TradingMarketComponent> market,
        TradingMarketOffer offer)
    {
        if (!market.Comp.Offers.Remove(offer.Id))
            return;

        RemoveOfferFromBook(
            market.Comp.Commodities[offer.CommodityId],
            offer.Side,
            offer.Price,
            offer.ParticipantKind == TradingParticipantKind.Guild,
            market.Comp.PriceWeightBase);
        if (offer.Pit is { } pit && TryComp<TradingComponent>(pit, out var trading))
            trading.MarketOffers.Remove(offer.Id);
    }

    private void TryRemoveCommodity(Entity<TradingMarketComponent> market, TradingCommodity commodity)
    {
        if (commodity.Permanent || market.Comp.Offers.Values.Any(offer => offer.CommodityId == commodity.Id))
            return;

        market.Comp.Commodities.Remove(commodity.Id);
    }

    private bool CanTradeItem(EntityUid item, TradingMarketConfigPrototype config)
    {
        if (!Exists(item) ||
            TerminatingOrDeleted(item) ||
            EntityManager.IsQueuedForDeletion(item) ||
            HasComp<TradingLotBlockedComponent>(item) ||
            MetaData(item).EntityPrototype is not { } prototype ||
            !prototype.HasComponent<ItemComponent>() ||
            !CanTradeProduct(prototype, config) ||
            ContainsPlayerMind(item))
        {
            return false;
        }

        return !TryComp<ExpendableLightComponent>(item, out var light) ||
               light.CurrentState == ExpendableLightState.BrandNew;
    }

    private void OnTradingLotBlockedStackSplit(
        EntityUid uid,
        TradingLotBlockedComponent component,
        ref StackSplitEvent args)
    {
        EnsureComp<TradingLotBlockedComponent>(args.NewId);
    }

    private void OnTradingLotBlockedExamined(
        EntityUid uid,
        TradingLotBlockedComponent component,
        ExaminedEvent args)
    {
        if (!HasComp<MedievalMoneyCheckerComponent>(args.Examiner))
            return;

        args.PushMarkup(Loc.GetString("trading-lot-blocked-examine"));
    }

    private bool CanTradeProduct(EntProtoId product, TradingMarketConfigPrototype config)
    {
        return _prototypeManager.TryIndex(product, out var prototype) &&
               CanTradeProduct(prototype, config);
    }

    private bool CanTradeProduct(EntityPrototype prototype, TradingMarketConfigPrototype config)
    {
        if (prototype.HasComponent<VirtualItemComponent>() ||
            prototype.HasComponent<MobStateComponent>() ||
            prototype.HasComponent<TimedDespawnComponent>() ||
            prototype.HasComponent<MedievalTimedDespawnComponent>() ||
            prototype.HasComponent<ActiveTimerTriggerComponent>() ||
            prototype.HasComponent<ActiveTwoStageTriggerComponent>() ||
            prototype.HasComponent<LetterComponent>() ||
            HasBlockedTraderProductTag(prototype, config))
        {
            return false;
        }

        return !prototype.TryGetComponent<ExpendableLightComponent>(
                   out var light,
                   EntityManager.ComponentFactory) ||
               light.CurrentState == ExpendableLightState.BrandNew;
    }

    private bool HasBlockedTraderProductTag(EntityPrototype prototype, TradingMarketConfigPrototype config)
    {
        return config.BlockedTraderItemTags.Count > 0 &&
               prototype.TryGetComponent<TagComponent>(out var tags, EntityManager.ComponentFactory) &&
               _tags.HasAnyTag(tags, config.BlockedTraderItemTags);
    }

    private bool ContainsPlayerMind(EntityUid root)
    {
        var pending = new Queue<EntityUid>();
        var visited = new HashSet<EntityUid>();
        pending.Enqueue(root);

        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current))
                continue;

            if (TryComp<MindContainerComponent>(current, out var mind) && mind.HasMind)
                return true;

            if (!TryComp<ContainerManagerComponent>(current, out var containerManager))
                continue;

            foreach (var container in _containers.GetAllContainers(current, containerManager))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    pending.Enqueue(contained);
                }
            }
        }

        return false;
    }

    internal bool TryResolveCommodityForItem(
        Entity<TradingMarketComponent> market,
        EntityUid item,
        int fallbackPrice,
        bool create,
        out TradingCommodity commodity,
        int? stackCountOverride = null,
        bool forceIntactEquipment = false)
    {
        commodity = default!;
        var config = _prototypeManager.Index(market.Comp.Config);
        if (!CanTradeItem(item, config))
            return false;

        if (HasComp<VirtualItemComponent>(item) ||
            MetaData(item).EntityPrototype?.ID is not { } product)
        {
            return false;
        }

        market.Comp.CommonCommodities.TryGetValue(product, out var commonId);
        TradingCommodity? common = null;
        var hasCommon = commonId != Guid.Empty && market.Comp.Commodities.TryGetValue(commonId, out common);
        var stackCount = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
        if (stackCountOverride is { } overrideCount)
        {
            if (stack == null || overrideCount <= 0 || overrideCount > stack.Count)
                return false;

            stackCount = overrideCount;
        }

        var hasStack = stack != null;
        var isRecipe = HasComp<MedievalRandomChemistryRecipeComponent>(item);
        var isCanvas = HasComp<CanvasComponent>(item);
        var hasStoredSolution = TryComp<SolutionContainerManagerComponent>(item, out var solutionManager) &&
                                _solutionContainer.EnumerateSolutions((item, solutionManager))
                                    .Any(solution => solution.Solution.Comp.Solution.Volume > 0);
        var hasCurrencyValue = HasComp<MedievalCurrencyComponent>(item);
        var hasAppliedQuality = TryComp<SmithQualityComponent>(item, out var quality) && quality.Applied;
        var isEquipment = hasAppliedQuality ||
                          HasComp<MedievalMeleeResourceComponent>(item) ||
                          HasComp<MedievalArmorIntegrityComponent>(item);
        var isDamagedEquipment = !forceIntactEquipment && IsDamagedEquipment(item);
        var matchesCommon = !isRecipe &&
                            !isCanvas &&
                            !hasStoredSolution &&
                            !hasCurrencyValue &&
                            hasCommon &&
                            common != null &&
                            common.HasStack == hasStack &&
                            common.BaselineStackCount == stackCount &&
                            (!isEquipment || !hasAppliedQuality && !isDamagedEquipment);

        if (matchesCommon)
        {
            commodity = common!;
            return true;
        }

        var signature = BuildItemSignature(item, product, stackCount, isEquipment, isDamagedEquipment);
        var existing = market.Comp.Commodities.Values.FirstOrDefault(value =>
            !value.Permanent && value.Signature == signature);
        if (existing != null)
        {
            existing.IsDamagedEquipment = isDamagedEquipment;
            commodity = existing;
            return true;
        }

        if (!create)
            return false;

        var standardPrice = hasCommon && common != null
            ? common.StandardPrice
            : Math.Max(fallbackPrice, 1);
        var metadata = MetaData(item);
        commodity = new TradingCommodity
        {
            Id = Guid.NewGuid(),
            Product = product,
            Sections = TradingMarketSection.Unique,
            StandardPrice = standardPrice,
            BaselineStackCount = stackCount,
            HasStack = hasStack,
            IsDamagedEquipment = isDamagedEquipment,
            Signature = signature,
            DisplayName = FormatStackName(metadata.EntityName, hasStack ? stackCount : null),
            Description = metadata.EntityDescription,
            Categories = hasCommon && common != null
                ? new HashSet<ProtoId<GuildTypePrototype>>(common.Categories)
                : new HashSet<ProtoId<GuildTypePrototype>>(),
        };
        market.Comp.Commodities.Add(commodity.Id, commodity);
        return true;
    }

    private bool IsDamagedEquipment(EntityUid item)
    {
        var equipment = false;
        var damaged = false;

        if (TryComp<MedievalMeleeResourceComponent>(item, out var weapon))
        {
            equipment = true;
            damaged |= weapon.Resource <= 80f;
        }

        if (TryComp<MedievalArmorIntegrityComponent>(item, out var armor))
        {
            equipment = true;
            damaged |= !MathHelper.CloseTo(armor.MaxArmorHP, armor.ContainerArmorHP) ||
                       !MathHelper.CloseTo(armor.CurrentArmorHP, armor.ContainerArmorHP);
        }

        return equipment && damaged;
    }

    private string BuildItemSignature(
        EntityUid item,
        EntProtoId product,
        int stackCount,
        bool isEquipment,
        bool isDamagedEquipment)
    {
        var values = new List<string>
        {
            product.Id,
            stackCount.ToString(CultureInfo.InvariantCulture),
        };

        if (isEquipment)
        {
            values.Add(isDamagedEquipment.ToString());
            values.Add(TryComp<SmithQualityComponent>(item, out var quality) && quality.Applied
                ? ((int) quality.Quality).ToString(CultureInfo.InvariantCulture)
                : "none");
            return string.Join('\u001f', values);
        }

        var metadata = MetaData(item);
        values.Add(metadata.EntityName);
        values.Add(metadata.EntityDescription);

        return string.Join('\u001f', values);
    }

    private string FormatStackName(string name, int? count)
    {
        if (count == null)
            return name;

        return Loc.GetString(
            "trading-ui-stack-name",
            ("name", name),
            ("count", count.Value));
    }
}
