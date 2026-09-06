using Content.Shared.Imperial.Medieval.Trading.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    private static void InitializeReputationScarcity(
        TradingMarketComponent market,
        TradingMarketConfigPrototype config)
    {
        var stepsPerPoint = GetReputationScarcityStepsPerPoint(config);
        foreach (var commodity in market.Commodities.Values)
        {
            commodity.InitialScarcitySteps = Math.Max(0, commodity.MinReputation * stepsPerPoint);
            commodity.RemainingScarcitySteps = commodity.InitialScarcitySteps;
        }
    }

    private static void AdvanceReputationScarcity(TradingMarketComponent market)
    {
        foreach (var commodity in market.Commodities.Values)
        {
            if (commodity.RemainingScarcitySteps > 0)
                commodity.RemainingScarcitySteps--;
        }
    }

    internal static int GetReputationScarcityStepsPerPoint(TradingMarketConfigPrototype config)
    {
        var secondsPerPoint = Math.Max(0f, config.ReputationScarcityMinutesPerPoint) * 60f;
        return Math.Max(1, (int) MathF.Ceiling(secondsPerPoint / Math.Max(float.Epsilon, config.StepInterval)));
    }

    internal static float GetReputationScarcityReferenceMultiplier(TradingCommodity commodity)
    {
        if (commodity.InitialScarcitySteps <= 0 || commodity.RemainingScarcitySteps <= 0)
            return 1f;

        var remainingRatio = (float) commodity.RemainingScarcitySteps / commodity.InitialScarcitySteps;
        return 1f + 0.9f * commodity.MinReputation * remainingRatio;
    }
}
