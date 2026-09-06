using Content.Shared.Imperial.Medieval.Trading;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    private static void RefreshMarketOrderBooks(TradingMarketComponent market)
    {
        foreach (var commodity in market.Commodities.Values)
        {
            EnsureCommodityBookBasis(
                commodity,
                GetGuildReferencePrice(commodity),
                market.PriceWeightBase);
        }
    }

    private static void AddOfferToBook(
        TradingCommodity commodity,
        TradingOfferSide side,
        int price,
        bool guildOffer,
        float priceWeightBase)
    {
        var book = GetOrderBook(commodity, side);
        EnsureOrderBookBasis(book, GetGuildReferencePrice(commodity), priceWeightBase);

        if (guildOffer)
            book.GuildOfferCount++;

        if (price <= 0)
            return;

        book.PriceLevels.TryGetValue(price, out var priceCount);
        book.PriceLevels[price] = priceCount + 1;
        AddWeightedPrice(book.Prices, price);
    }

    private static void RemoveOfferFromBook(
        TradingCommodity commodity,
        TradingOfferSide side,
        int price,
        bool guildOffer,
        float priceWeightBase)
    {
        var book = GetOrderBook(commodity, side);
        EnsureOrderBookBasis(book, GetGuildReferencePrice(commodity), priceWeightBase);

        if (guildOffer)
            book.GuildOfferCount--;

        if (price <= 0)
            return;

        RemovePriceLevel(book, price);
    }

    private static void EnsureCommodityBookBasis(
        TradingCommodity commodity,
        float referencePrice,
        float priceWeightBase)
    {
        EnsureOrderBookBasis(commodity.BuyBook, referencePrice, priceWeightBase);
        EnsureOrderBookBasis(commodity.SellBook, referencePrice, priceWeightBase);
    }

    private static void EnsureOrderBookBasis(
        TradingOrderBook book,
        float referencePrice,
        float priceWeightBase)
    {
        if (book.Prices.ReferencePrice == referencePrice &&
            book.Prices.PriceWeightBase == priceWeightBase)
        {
            return;
        }

        RebuildOrderBook(book, referencePrice, priceWeightBase);
    }

    private static void RebuildOrderBook(
        TradingOrderBook book,
        float referencePrice,
        float priceWeightBase)
    {
        ValidatePriceBasis(referencePrice, priceWeightBase);
        ResetPriceAggregate(book.Prices, referencePrice, priceWeightBase);

        foreach (var (price, count) in book.PriceLevels)
        {
            if (count <= 0)
                continue;

            book.Prices.Count += count;
            book.Prices.MaximumLogWeight = MathF.Max(
                book.Prices.MaximumLogWeight,
                GetLogPriceWeight(price, referencePrice, priceWeightBase));
        }

        if (book.Prices.Count == 0)
            return;

        foreach (var (price, count) in book.PriceLevels)
        {
            if (count <= 0)
                continue;

            var weight = Math.Exp(
                GetLogPriceWeight(price, referencePrice, priceWeightBase) -
                book.Prices.MaximumLogWeight);
            book.Prices.ScaledWeightSum += count * weight;
            book.Prices.ScaledPriceSum += count * price * weight;
        }

        UpdateAveragePrice(book.Prices);
    }

    private static void AddWeightedPrice(TradingPriceAggregate aggregate, int price)
    {
        var logWeight = GetLogPriceWeight(price, aggregate.ReferencePrice, aggregate.PriceWeightBase);
        if (aggregate.Count == 0)
        {
            aggregate.MaximumLogWeight = logWeight;
            aggregate.ScaledWeightSum = 1d;
            aggregate.ScaledPriceSum = price;
            aggregate.Count = 1;
            aggregate.AveragePrice = price;
            return;
        }

        if (logWeight > aggregate.MaximumLogWeight)
        {
            var scale = Math.Exp(aggregate.MaximumLogWeight - logWeight);
            aggregate.ScaledWeightSum = aggregate.ScaledWeightSum * scale + 1d;
            aggregate.ScaledPriceSum = aggregate.ScaledPriceSum * scale + price;
            aggregate.MaximumLogWeight = logWeight;
        }
        else
        {
            var weight = Math.Exp(logWeight - aggregate.MaximumLogWeight);
            aggregate.ScaledWeightSum += weight;
            aggregate.ScaledPriceSum += price * weight;
        }

        aggregate.Count++;
        UpdateAveragePrice(aggregate);
    }

    private static void RemovePriceLevel(TradingOrderBook book, int price)
    {
        if (!book.PriceLevels.TryGetValue(price, out var priceCount) || priceCount <= 0)
            throw new InvalidOperationException($"Trading order book does not contain price level {price}.");

        var removesMaximum = priceCount == 1 &&
                             GetLogPriceWeight(
                                 price,
                                 book.Prices.ReferencePrice,
                                 book.Prices.PriceWeightBase) == book.Prices.MaximumLogWeight;

        if (priceCount == 1)
            book.PriceLevels.Remove(price);
        else
            book.PriceLevels[price] = priceCount - 1;

        if (book.Prices.Count == 1)
        {
            ResetPriceAggregate(
                book.Prices,
                book.Prices.ReferencePrice,
                book.Prices.PriceWeightBase);
            return;
        }

        if (removesMaximum)
        {
            RebuildOrderBook(
                book,
                book.Prices.ReferencePrice,
                book.Prices.PriceWeightBase);
            return;
        }

        RemoveWeightedPrice(book.Prices, price);
    }

    private static void RemoveWeightedPrice(TradingPriceAggregate aggregate, int price)
    {
        var weight = Math.Exp(
            GetLogPriceWeight(price, aggregate.ReferencePrice, aggregate.PriceWeightBase) -
            aggregate.MaximumLogWeight);
        aggregate.ScaledWeightSum -= weight;
        aggregate.ScaledPriceSum -= price * weight;
        aggregate.Count--;
        UpdateAveragePrice(aggregate);
    }

    private static float GetCachedMarketPrice(TradingCommodity commodity)
    {
        if (commodity.BuyBook.Prices.Count == 0 || commodity.SellBook.Prices.Count == 0)
            return float.NaN;

        return (commodity.BuyBook.Prices.AveragePrice + commodity.SellBook.Prices.AveragePrice) / 2f;
    }

    private static float GetProjectedMarketPrice(
        TradingCommodity commodity,
        TradingOfferSide side,
        int addedPrice,
        int? removedPrice)
    {
        var changedBook = GetOrderBook(commodity, side);
        var projectedAverage = GetProjectedAveragePrice(changedBook, addedPrice, removedPrice);
        var buyAverage = side == TradingOfferSide.Buy
            ? projectedAverage
            : commodity.BuyBook.Prices.AveragePrice;
        var sellAverage = side == TradingOfferSide.Sell
            ? projectedAverage
            : commodity.SellBook.Prices.AveragePrice;

        if (!float.IsFinite(buyAverage) || !float.IsFinite(sellAverage))
            return float.NaN;

        return (buyAverage + sellAverage) / 2f;
    }

    private static float GetProjectedAveragePrice(
        TradingOrderBook book,
        int addedPrice,
        int? removedPrice)
    {
        if (removedPrice is { } removed)
        {
            if (!book.PriceLevels.TryGetValue(removed, out var count) || count <= 0)
                throw new InvalidOperationException($"Trading order book does not contain price level {removed}.");

            if (count == 1 &&
                GetLogPriceWeight(
                    removed,
                    book.Prices.ReferencePrice,
                    book.Prices.PriceWeightBase) == book.Prices.MaximumLogWeight)
            {
                return CalculateProjectedAveragePrice(book, addedPrice, removed);
            }
        }

        var projection = CopyPriceAggregate(book.Prices);
        if (removedPrice is { } removedPriceValue)
            RemoveWeightedPrice(projection, removedPriceValue);
        AddWeightedPrice(projection, addedPrice);
        return projection.AveragePrice;
    }

    private static float CalculateProjectedAveragePrice(
        TradingOrderBook book,
        int addedPrice,
        int removedPrice)
    {
        var addedLogWeight = GetLogPriceWeight(
            addedPrice,
            book.Prices.ReferencePrice,
            book.Prices.PriceWeightBase);
        var maximumLogWeight = addedLogWeight;

        foreach (var (price, count) in book.PriceLevels)
        {
            var projectedCount = count - (price == removedPrice ? 1 : 0);
            if (projectedCount <= 0)
                continue;

            maximumLogWeight = MathF.Max(
                maximumLogWeight,
                GetLogPriceWeight(
                    price,
                    book.Prices.ReferencePrice,
                    book.Prices.PriceWeightBase));
        }

        var addedWeight = Math.Exp(addedLogWeight - maximumLogWeight);
        var weightSum = addedWeight;
        var priceSum = addedPrice * addedWeight;
        foreach (var (price, count) in book.PriceLevels)
        {
            var projectedCount = count - (price == removedPrice ? 1 : 0);
            if (projectedCount <= 0)
                continue;

            var weight = Math.Exp(
                GetLogPriceWeight(
                    price,
                    book.Prices.ReferencePrice,
                    book.Prices.PriceWeightBase) -
                maximumLogWeight);
            weightSum += projectedCount * weight;
            priceSum += projectedCount * price * weight;
        }

        return (float) (priceSum / weightSum);
    }

    private static TradingPriceAggregate CopyPriceAggregate(TradingPriceAggregate source)
    {
        return new TradingPriceAggregate
        {
            Count = source.Count,
            AveragePrice = source.AveragePrice,
            ReferencePrice = source.ReferencePrice,
            PriceWeightBase = source.PriceWeightBase,
            MaximumLogWeight = source.MaximumLogWeight,
            ScaledWeightSum = source.ScaledWeightSum,
            ScaledPriceSum = source.ScaledPriceSum,
        };
    }

    private static TradingOrderBook GetOrderBook(
        TradingCommodity commodity,
        TradingOfferSide side)
    {
        return side == TradingOfferSide.Buy ? commodity.BuyBook : commodity.SellBook;
    }

    private static void ResetPriceAggregate(
        TradingPriceAggregate aggregate,
        float referencePrice,
        float priceWeightBase)
    {
        aggregate.Count = 0;
        aggregate.AveragePrice = float.NaN;
        aggregate.ReferencePrice = referencePrice;
        aggregate.PriceWeightBase = priceWeightBase;
        aggregate.MaximumLogWeight = float.NegativeInfinity;
        aggregate.ScaledWeightSum = 0d;
        aggregate.ScaledPriceSum = 0d;
    }

    private static void UpdateAveragePrice(TradingPriceAggregate aggregate)
    {
        aggregate.AveragePrice = aggregate.Count > 0 && aggregate.ScaledWeightSum > 0d
            ? (float) (aggregate.ScaledPriceSum / aggregate.ScaledWeightSum)
            : float.NaN;
    }

    private static float GetLogPriceWeight(
        float price,
        float referencePrice,
        float priceWeightBase)
    {
        return (GetDistanceRatio(price, referencePrice) - 1f) * MathF.Log(priceWeightBase);
    }

    private static void ValidatePriceBasis(float referencePrice, float priceWeightBase)
    {
        if (!float.IsFinite(referencePrice) || referencePrice <= 0f)
            throw new ArgumentOutOfRangeException(nameof(referencePrice));

        if (!float.IsFinite(priceWeightBase) || priceWeightBase <= 0f || priceWeightBase > 1f)
            throw new ArgumentOutOfRangeException(nameof(priceWeightBase));
    }
}
