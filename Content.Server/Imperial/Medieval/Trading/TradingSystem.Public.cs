using System.Linq;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Store;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    private void InitializePublicTrading()
    {
        SubscribeLocalEvent<ContainerManagerComponent, EntInsertedIntoContainerMessage>(OnPublicContainerInserted);
        SubscribeLocalEvent<ContainerManagerComponent, EntRemovedFromContainerMessage>(OnPublicContainerRemoved);
        SubscribeLocalEvent<PublicTradingBalanceComponent, ComponentShutdown>(OnPublicTradingBalanceShutdown);
        SubscribeLocalEvent<PublicTradingCurrencyTrackerComponent, StackCountChangedEvent>(OnPublicStackCountChanged);
        SubscribeLocalEvent<PublicTradingBalanceRefreshEvent>(OnPublicTradingBalanceRefresh);
    }

    private void OnPublicTradingBalanceRefresh(PublicTradingBalanceRefreshEvent args)
    {
        if (!TryComp<PublicTradingBalanceComponent>(args.User, out var component))
            return;

        component.RefreshQueued = false;
        component.OpenPits.RemoveWhere(pit =>
            !Exists(pit) ||
            !HasComp<PublicTradingPitComponent>(pit) ||
            !_ui.IsUiOpen(pit, TradingUiKey.Key, args.User));
        if (component.OpenPits.Count == 0)
        {
            RemCompDeferred<PublicTradingBalanceComponent>(args.User);
            return;
        }

        if (!component.BalanceDirty || !RefreshPublicTradingBalances(args.User, component))
            return;

        foreach (var pit in component.OpenPits)
        {
            if (TryComp<TradingComponent>(pit, out var trading))
                UpdateUserInterface(args.User, pit, trading);
        }
    }

    private void OpenPublicTradingSession(EntityUid pit, EntityUid user)
    {
        if (!HasComp<PublicTradingPitComponent>(pit))
            return;

        var balance = EnsureComp<PublicTradingBalanceComponent>(user);
        balance.OpenPits.Add(pit);
        RefreshPublicTradingBalances(user, balance);
    }

    private void ClosePublicTradingSession(EntityUid pit, EntityUid user)
    {
        if (!TryComp<PublicTradingBalanceComponent>(user, out var balance))
            return;

        balance.OpenPits.Remove(pit);
        if (balance.OpenPits.Count == 0)
            RemCompDeferred<PublicTradingBalanceComponent>(user);
    }

    private void RemovePublicTradingSessions(EntityUid pit)
    {
        var query = EntityQueryEnumerator<PublicTradingBalanceComponent>();
        while (query.MoveNext(out var user, out var component))
        {
            component.OpenPits.Remove(pit);
            if (component.OpenPits.Count == 0)
                RemCompDeferred<PublicTradingBalanceComponent>(user);
        }
    }

    private int GetPublicTradingBalance(
        EntityUid user,
        EntityUid pit,
        ProtoId<CurrencyPrototype> currency)
    {
        var component = EnsureComp<PublicTradingBalanceComponent>(user);
        component.OpenPits.Add(pit);
        if (component.BalanceDirty || !component.Balances.ContainsKey(currency))
            RefreshPublicTradingBalances(user, component);

        return component.Balances.GetValueOrDefault(currency);
    }

    private bool RefreshPublicTradingBalances(EntityUid user, PublicTradingBalanceComponent component)
    {
        component.OpenPits.RemoveWhere(pit => !Exists(pit) || !HasComp<PublicTradingPitComponent>(pit));
        var balances = new Dictionary<ProtoId<CurrencyPrototype>, int>();
        var trackedCurrencyStacks = new HashSet<EntityUid>();
        foreach (var pit in component.OpenPits)
        {
            if (!TryComp<TradingComponent>(pit, out var trading) ||
                balances.ContainsKey(trading.Currency))
            {
                continue;
            }

            balances[trading.Currency] = CountInventoryCurrency(user, trading.Currency, trackedCurrencyStacks);
        }

        UpdatePublicCurrencyTrackers(user, component, trackedCurrencyStacks);

        var changed = component.Balances.Count != balances.Count ||
                      component.Balances.Any(entry =>
                          !balances.TryGetValue(entry.Key, out var value) || value != entry.Value);
        component.Balances = balances;
        component.BalanceDirty = false;
        return changed;
    }

    private int CountInventoryCurrency(
        EntityUid user,
        ProtoId<CurrencyPrototype> currency,
        HashSet<EntityUid> trackedCurrencyStacks)
    {
        long total = 0;
        foreach (var item in GetPublicInventoryItems(user))
        {
            if (!TryGetCurrencyUnitValue(item, currency, out var unitValue))
                continue;

            var count = 1;
            if (TryComp<StackComponent>(item, out var stack))
            {
                count = stack.Count;
                trackedCurrencyStacks.Add(item);
                EnsureComp<PublicTradingCurrencyTrackerComponent>(item).User = user;
            }

            total = Math.Min(int.MaxValue, total + (long) unitValue * count);
        }

        return (int) total;
    }

    private bool TrySpendPublicCurrency(EntityUid user, ProtoId<CurrencyPrototype> currency, int amount)
    {
        if (amount < 0)
            return false;

        if (amount == 0)
            return true;

        var currencyItems = GetPublicInventoryItems(user)
            .Where(item => TryGetCurrencyUnitValue(item, currency, out var value) && value == 1)
            .ToList();
        var available = currencyItems.Sum(item => (long) (TryComp<StackComponent>(item, out var stack) ? stack.Count : 1));
        if (available < amount)
            return false;

        var remaining = amount;
        foreach (var item in currencyItems)
        {
            if (remaining == 0)
                break;

            if (TryComp<StackComponent>(item, out var stack))
            {
                var used = Math.Min(stack.Count, remaining);
                _stack.SetCount(item, stack.Count - used, stack);
                remaining -= used;
                continue;
            }

            QueueDel(item);
            remaining--;
        }

        if (TryComp<PublicTradingBalanceComponent>(user, out var balance))
        {
            balance.BalanceDirty = true;
            RefreshPublicTradingBalances(user, balance);
        }

        return remaining == 0;
    }

    private bool TryGetCurrencyUnitValue(
        EntityUid item,
        ProtoId<CurrencyPrototype> currency,
        out int unitValue)
    {
        unitValue = 0;
        if (!Exists(item) ||
            TerminatingOrDeleted(item) ||
            EntityManager.IsQueuedForDeletion(item) ||
            !TryComp<MedievalCurrencyComponent>(item, out var medievalCurrency) ||
            !medievalCurrency.Price.TryGetValue(currency.Id, out var price))
        {
            return false;
        }

        unitValue = price.Int();
        return unitValue > 0;
    }

    private List<EntityUid> GetPublicInventoryItems(EntityUid user)
    {
        var items = new List<EntityUid>();
        var pending = new Queue<EntityUid>(_inventory.GetHandOrInventoryEntities(user));
        var visited = new HashSet<EntityUid>();
        while (pending.TryDequeue(out var candidate))
        {
            if (!visited.Add(candidate) ||
                !Exists(candidate) ||
                TerminatingOrDeleted(candidate) ||
                EntityManager.IsQueuedForDeletion(candidate))
            {
                continue;
            }

            items.Add(candidate);
            if (!TryComp<ContainerManagerComponent>(candidate, out var containerManager))
                continue;

            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    pending.Enqueue(contained);
                }
            }
        }

        return items;
    }

    private void UpdatePublicCurrencyTrackers(
        EntityUid user,
        PublicTradingBalanceComponent component,
        HashSet<EntityUid> trackedCurrencyStacks)
    {
        foreach (var stack in component.TrackedCurrencyStacks)
        {
            if (trackedCurrencyStacks.Contains(stack) ||
                !TryComp<PublicTradingCurrencyTrackerComponent>(stack, out var tracker) ||
                tracker.User != user)
            {
                continue;
            }

            RemComp<PublicTradingCurrencyTrackerComponent>(stack);
        }

        component.TrackedCurrencyStacks = trackedCurrencyStacks;
    }

    private void OnPublicTradingBalanceShutdown(
        Entity<PublicTradingBalanceComponent> entity,
        ref ComponentShutdown args)
    {
        foreach (var stack in entity.Comp.TrackedCurrencyStacks)
        {
            if (TryComp<PublicTradingCurrencyTrackerComponent>(stack, out var tracker) &&
                tracker.User == entity.Owner)
            {
                RemComp<PublicTradingCurrencyTrackerComponent>(stack);
            }
        }
    }

    private void OnPublicContainerInserted(
        Entity<ContainerManagerComponent> entity,
        ref EntInsertedIntoContainerMessage args)
    {
        MarkPublicTradingBalanceDirty(entity.Owner);
    }

    private void OnPublicContainerRemoved(
        Entity<ContainerManagerComponent> entity,
        ref EntRemovedFromContainerMessage args)
    {
        MarkPublicTradingBalanceDirty(entity.Owner);
    }

    private void OnPublicStackCountChanged(
        Entity<PublicTradingCurrencyTrackerComponent> entity,
        ref StackCountChangedEvent args)
    {
        if (TryComp<PublicTradingBalanceComponent>(entity.Comp.User, out var balance))
            QueuePublicTradingBalanceRefresh(entity.Comp.User, balance);
    }

    private void MarkPublicTradingBalanceDirty(EntityUid item)
    {
        var current = item;
        if (TryComp<PublicTradingBalanceComponent>(current, out var direct))
        {
            QueuePublicTradingBalanceRefresh(current, direct);
            return;
        }

        while (_containers.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;
            if (!TryComp<PublicTradingBalanceComponent>(current, out var balance))
                continue;

            QueuePublicTradingBalanceRefresh(current, balance);
            return;
        }
    }

    private void QueuePublicTradingBalanceRefresh(
        EntityUid user,
        PublicTradingBalanceComponent component)
    {
        component.BalanceDirty = true;
        if (component.RefreshQueued)
            return;

        component.RefreshQueued = true;
        QueueLocalEvent(new PublicTradingBalanceRefreshEvent(user));
    }

    private void BuyPublicCommodity(
        EntityUid pit,
        TradingComponent trading,
        EntityUid buyer,
        Guid commodityId)
    {
        if (!TryGetMarket(out var market))
            return;

        var ask = GetLowestPublicSellOffer(market.Comp.Offers.Values, commodityId);
        if (ask != null)
            BuyPublicOffer(pit, trading, buyer, market, ask);
    }

    private void BuyPublicOffer(
        EntityUid pit,
        TradingComponent trading,
        EntityUid buyer,
        Guid offerId)
    {
        if (!TryGetMarket(out var market) || !market.Comp.Offers.TryGetValue(offerId, out var ask))
            return;

        BuyPublicOffer(pit, trading, buyer, market, ask);
    }

    private void BuyPublicOffer(
        EntityUid pit,
        TradingComponent trading,
        EntityUid buyer,
        Entity<TradingMarketComponent> market,
        TradingMarketOffer ask)
    {
        if (!market.Comp.Offers.TryGetValue(ask.Id, out var currentAsk) ||
            currentAsk != ask ||
            ask.Side != TradingOfferSide.Sell ||
            ask.ParticipantKind != TradingParticipantKind.Trader ||
            ask.Pit == pit ||
            !market.Comp.Commodities.TryGetValue(ask.CommodityId, out var commodity))
        {
            return;
        }

        var config = _prototypeManager.Index(market.Comp.Config);
        if (ask.Item is not { } item || !CanTradeItem(item, config))
        {
            RemoveOffer(market, ask.Id, true);
            UpdateAllInterfaces(market);
            return;
        }

        if (!CanTransferEscrowItem(ask, item))
        {
            RemoveOffer(market, ask.Id, false);
            UpdateAllInterfaces(market);
            return;
        }

        if (!TrySpendPublicCurrency(buyer, trading.Currency, ask.Price))
        {
            ShowInsufficientPurchaseFunds(buyer);
            UpdateUserInterface(buyer, pit, trading);
            return;
        }

        var bid = new TradingMarketOffer
        {
            Id = Guid.NewGuid(),
            CommodityId = commodity.Id,
            Product = commodity.Product,
            Side = TradingOfferSide.Buy,
            ParticipantKind = TradingParticipantKind.Trader,
            ParticipantName = MetaData(buyer).EntityName,
            Price = ask.Price,
            Pit = pit,
            ImmediateRecipient = buyer,
            IsImmediate = true,
            UsesExternalFunds = true,
            Sequence = market.Comp.NextSequence++,
        };
        AddOffer(market, bid);
        CompleteTrade(market, commodity, ask, bid, config);
        ShowTradingSuccess(buyer, pit, trading, "trading-ui-purchase-success");
        UpdateAllInterfaces(market);
    }

    private static TradingMarketOffer? GetLowestPublicSellOffer(
        IEnumerable<TradingMarketOffer> offers,
        Guid commodityId)
    {
        return offers
            .Where(offer => offer.CommodityId == commodityId &&
                            offer.Side == TradingOfferSide.Sell &&
                            offer.ParticipantKind == TradingParticipantKind.Trader)
            .OrderBy(offer => offer.Price)
            .ThenBy(offer => offer.Sequence)
            .FirstOrDefault();
    }

    private void ShowInsufficientPurchaseFunds(EntityUid actor)
    {
        _popup.PopupCursor(
            Loc.GetString("trading-ui-insufficient-purchase-funds"),
            actor,
            PopupType.SmallCaution);
    }
}

internal sealed class PublicTradingBalanceRefreshEvent(EntityUid user) : EntityEventArgs
{
    public readonly EntityUid User = user;
}
