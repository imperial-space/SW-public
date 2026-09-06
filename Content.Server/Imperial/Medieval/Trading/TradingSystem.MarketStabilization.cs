using System.Linq;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    private void CreateGuildInterventions(
        Entity<TradingMarketComponent> market,
        TradingMarketConfigPrototype config)
    {
        foreach (var commodityId in market.Comp.CommonCommodities.Values)
        {
            if (market.Comp.Commodities.TryGetValue(commodityId, out var commodity))
                TryCreateGuildIntervention(market, commodity, config);
        }
    }

    private void RemoveUncompetitiveGuildOffers(
        Entity<TradingMarketComponent> market,
        TradingMarketConfigPrototype config)
    {
        foreach (var commodityId in market.Comp.CommonCommodities.Values)
        {
            if (!market.Comp.Commodities.TryGetValue(commodityId, out var commodity))
                continue;

            var referencePrice = GetGuildReferencePrice(commodity);
            var marketPrice = GetCachedMarketPrice(commodity);
            if (!float.IsFinite(marketPrice))
                continue;

            TryRemoveUncompetitiveGuildOffer(
                market,
                commodity,
                TradingOfferSide.Sell,
                marketPrice,
                config.GuildOfferRemovalChanceScale);
            TryRemoveUncompetitiveGuildOffer(
                market,
                commodity,
                TradingOfferSide.Buy,
                marketPrice,
                config.GuildOfferRemovalChanceScale);
        }
    }

    private void TryRemoveUncompetitiveGuildOffer(
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity,
        TradingOfferSide side,
        float marketPrice,
        float chanceScale)
    {
        var offer = GetReplaceableGuildOffer(market, commodity, side);
        if (offer == null ||
            _random.NextFloat() >= GetGuildOfferRemovalChance(offer.Price, marketPrice, chanceScale))
        {
            return;
        }

        RemoveOffer(market, offer.Id, false);
    }

    private void TryCreateGuildIntervention(
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity,
        TradingMarketConfigPrototype config)
    {
        var referencePrice = GetGuildReferencePrice(commodity);
        var buyCount = commodity.BuyBook.Prices.Count;
        var sellCount = commodity.SellBook.Prices.Count;
        var referenceOfferCount = GetGuildOfferTarget(commodity, config);
        var missingSide = GetMissingBookSide(
            buyCount,
            sellCount,
            config.MaximumGuildBuyOrderCount,
            config.MaximumGuildSellOfferCount);
        if (missingSide is { } recoverySide)
        {
            RestoreGuildBookSide(market, commodity, recoverySide, referencePrice, config);
            return;
        }

        var marketPrice = GetCachedMarketPrice(commodity);
        if ((GetInterventionSide(marketPrice, referencePrice) ??
             GetLiquidityInterventionSide(
                 buyCount,
                 sellCount,
                 referenceOfferCount,
                 config.MaximumGuildBuyOrderCount,
                 config.MaximumGuildSellOfferCount)) is not { } side)
        {
            return;
        }

        var maximumOffers = side == TradingOfferSide.Sell
            ? config.MaximumGuildSellOfferCount
            : config.MaximumGuildBuyOrderCount;
        if (maximumOffers <= 0)
            return;

        var price = GetGuildInterventionPrice(
            marketPrice,
            referencePrice,
            config.InterventionCorrectionStrength);

        TradingMarketOffer? replaceable = null;
        if (GetGuildOfferCount(commodity, side) >= maximumOffers)
        {
            replaceable = GetReplaceableGuildOffer(market, commodity, side);
            if (replaceable == null || !IsMoreCompetitivePrice(price, replaceable.Price, side))
                return;
        }

        var currentOfferCount = side == TradingOfferSide.Buy
            ? buyCount
            : sellCount;
        var quantityChance = replaceable == null
            ? GetQuantityInterventionChance(currentOfferCount, referenceOfferCount)
            : 0f;

        var updatedMarketPrice = GetProjectedMarketPrice(
            commodity,
            side,
            price,
            replaceable?.Price);
        if (!MovesMarketTowardReference(marketPrice, updatedMarketPrice, referencePrice) &&
            (marketPrice != referencePrice || quantityChance <= 0f))
        {
            return;
        }

        var candidates = GetGuildCandidates(market, commodity);
        if (candidates.Count == 0)
            return;

        var chance = Math.Clamp(
            GetInterventionChance(
                marketPrice,
                referencePrice,
                config.InterventionChanceScale) + quantityChance,
            0f,
            1f);
        if (_random.NextFloat() >= chance)
            return;

        if (replaceable != null)
            RemoveOffer(market, replaceable.Id, false);

        CreateGuildOffer(
            market,
            _random.Pick(candidates),
            commodity,
            side,
            price);
    }

    private void RestoreGuildBookSide(
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity,
        TradingOfferSide side,
        float referencePrice,
        TradingMarketConfigPrototype config)
    {
        var candidates = GetGuildCandidates(market, commodity);
        if (candidates.Count == 0)
            return;

        var price = RoundInitialGuildOfferPrice(
            GetInitialGuildOfferPrice(
                referencePrice,
                side,
                config.InitialGuildPriceSpread,
                0f),
            side);
        CreateGuildOffer(
            market,
            _random.Pick(candidates),
            commodity,
            side,
            price);
    }

    private static int GetGuildOfferCount(TradingCommodity commodity, TradingOfferSide side)
    {
        return GetOrderBook(commodity, side).GuildOfferCount;
    }

    private static TradingMarketOffer? GetReplaceableGuildOffer(
        Entity<TradingMarketComponent> market,
        TradingCommodity commodity,
        TradingOfferSide side)
    {
        var offers = market.Comp.Offers.Values.Where(offer =>
            offer.CommodityId == commodity.Id &&
            offer.ParticipantKind == TradingParticipantKind.Guild &&
            offer.Side == side);
        return side == TradingOfferSide.Sell
            ? offers.OrderByDescending(offer => offer.Price).ThenBy(offer => offer.Sequence).FirstOrDefault()
            : offers.OrderBy(offer => offer.Price).ThenBy(offer => offer.Sequence).FirstOrDefault();
    }

    internal static bool IsMoreCompetitivePrice(
        int candidatePrice,
        int currentPrice,
        TradingOfferSide side)
    {
        return side == TradingOfferSide.Sell
            ? candidatePrice < currentPrice
            : candidatePrice > currentPrice;
    }

    internal static float GetGuildOfferRemovalChance(
        float offerPrice,
        float marketPrice,
        float chanceScale)
    {
        if (!float.IsFinite(offerPrice) ||
            !float.IsFinite(marketPrice) ||
            offerPrice <= 0f ||
            marketPrice <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(
            MathF.Max(0f, chanceScale) * GetDistanceRatio(offerPrice, marketPrice),
            0f,
            1f);
    }

    internal static int GetGuildInterventionPrice(
        float marketPrice,
        float referencePrice,
        float correctionStrength)
    {
        return RoundMarketPrice(GetInternalOrderPrice(
            marketPrice,
            referencePrice,
            correctionStrength));
    }

    internal static TradingOfferSide? GetInterventionSide(
        float marketPrice,
        float referencePrice)
    {
        if (!float.IsFinite(marketPrice) ||
            !float.IsFinite(referencePrice) ||
            marketPrice <= 0f ||
            referencePrice <= 0f)
        {
            return null;
        }

        if (marketPrice > referencePrice)
            return TradingOfferSide.Sell;

        if (marketPrice < referencePrice)
            return TradingOfferSide.Buy;

        return null;
    }

    internal static TradingOfferSide? GetMissingBookSide(
        int buyCount,
        int sellCount,
        int maximumBuyCount,
        int maximumSellCount)
    {
        if (sellCount <= 0 && maximumSellCount > 0)
            return TradingOfferSide.Sell;

        if (buyCount <= 0 && maximumBuyCount > 0)
            return TradingOfferSide.Buy;

        return null;
    }

    internal static TradingOfferSide? GetLiquidityInterventionSide(
        int buyCount,
        int sellCount,
        int referenceCount,
        int maximumBuyCount,
        int maximumSellCount)
    {
        var buyChance = maximumBuyCount > 0
            ? GetQuantityInterventionChance(buyCount, referenceCount)
            : 0f;
        var sellChance = maximumSellCount > 0
            ? GetQuantityInterventionChance(sellCount, referenceCount)
            : 0f;
        if (buyChance <= 0f && sellChance <= 0f)
            return null;

        return sellChance >= buyChance
            ? TradingOfferSide.Sell
            : TradingOfferSide.Buy;
    }

    internal static float GetGuildReferencePrice(TradingCommodity commodity)
    {
        return MathF.Max(1f, commodity.StandardPrice) * GetReputationScarcityReferenceMultiplier(commodity);
    }

    internal static float GetDistanceRatio(float price, float referencePrice)
    {
        if (!float.IsFinite(price) ||
            !float.IsFinite(referencePrice) ||
            price <= 0f ||
            referencePrice <= 0f)
        {
            throw new ArgumentOutOfRangeException();
        }

        return MathF.Max(price / referencePrice, referencePrice / price);
    }

    internal static bool MovesMarketTowardReference(
        float marketPrice,
        float updatedMarketPrice,
        float referencePrice)
    {
        if (!float.IsFinite(marketPrice) ||
            !float.IsFinite(updatedMarketPrice) ||
            !float.IsFinite(referencePrice) ||
            marketPrice <= 0f ||
            updatedMarketPrice <= 0f ||
            referencePrice <= 0f)
        {
            return false;
        }

        return GetDistanceRatio(updatedMarketPrice, referencePrice) <
               GetDistanceRatio(marketPrice, referencePrice);
    }

    internal static float GetInterventionChance(
        float marketPrice,
        float referencePrice,
        float chanceScale)
    {
        var ratio = GetDistanceRatio(marketPrice, referencePrice);
        return Math.Clamp(MathF.Max(0f, chanceScale) * (ratio - 1f), 0f, 1f);
    }

    internal static float GetQuantityInterventionChance(
        int currentCount,
        int referenceCount)
    {
        if (referenceCount <= 0 || currentCount >= referenceCount)
            return 0f;

        if (currentCount <= 0)
            return 1f;

        var deficit = 1f - currentCount / (float) referenceCount;
        return deficit / 2f;
    }

    internal static float GetInternalOrderPrice(
        float marketPrice,
        float referencePrice,
        float correctionStrength)
    {
        if (!float.IsFinite(marketPrice) ||
            !float.IsFinite(referencePrice) ||
            marketPrice <= 0f ||
            referencePrice <= 0f)
        {
            throw new ArgumentOutOfRangeException();
        }

        var strength = Math.Clamp(correctionStrength, 0f, 1f);
        return marketPrice + strength * (referencePrice - marketPrice);
    }

    internal static float GetInitialGuildOfferPrice(
        float referencePrice,
        TradingOfferSide side,
        float spread,
        float depth)
    {
        if (!float.IsFinite(referencePrice) || referencePrice <= 0f)
            throw new ArgumentOutOfRangeException(nameof(referencePrice));

        var halfSpread = Math.Clamp(spread, 0f, 2f) / 2f;
        var priceDepth = Math.Clamp(depth, 0f, MathF.Max(0f, 1f - halfSpread));
        var factor = side == TradingOfferSide.Sell
            ? 1f + halfSpread + priceDepth
            : 1f - halfSpread - priceDepth;
        return referencePrice * factor;
    }

    internal static float GetInitialGuildOfferDepth(
        int index,
        int count,
        float maximumDepth)
    {
        if (index < 0 || count <= 0 || index >= count)
            throw new ArgumentOutOfRangeException();

        if (count == 1)
            return 0f;

        return MathF.Max(0f, maximumDepth) * index / (count - 1f);
    }

    internal static int RoundInitialGuildOfferPrice(float price, TradingOfferSide side)
    {
        var rounded = side == TradingOfferSide.Sell
            ? MathF.Ceiling(price)
            : MathF.Floor(price);
        return RoundMarketPrice(rounded);
    }
}
