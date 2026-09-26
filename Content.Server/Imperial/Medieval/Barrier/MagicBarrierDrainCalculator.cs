using Content.Server.MagicBarrier.Components;

namespace Content.Server.MagicBarrier;

public static class MagicBarrierDrainCalculator
{
    public static float Calculate(MagicBarrierComponent comp, int growthCount, int riftCount, int playerCount)
    {
        var baseCurseDrain = comp.BaseCurseDrain;
        var riftCurseDrain = comp.RiftCurseDrain;

        var lowHardCap = comp.HLCurseLimit;
        var highHardCap = comp.HHCurseLimit;

        if (playerCount < 30)
        {
            baseCurseDrain = 0.5f;
            riftCurseDrain = 1f;
            lowHardCap = 4f;
            highHardCap = 4f;
        }
        else if (playerCount < 60)
        {
            baseCurseDrain = 1f;
            riftCurseDrain = 2f;
            lowHardCap = 4f;
            highHardCap = 8f;
        }

        var lowCurse = (baseCurseDrain + riftCurseDrain * riftCount) *
        MathF.Pow(comp.MagicBarrierCurseEffect, 1f + growthCount);

        var totalSources = growthCount + riftCount;

        var highCurse = totalSources <= comp.ACurseLimit ? lowHardCap : lowHardCap +
        (highHardCap - lowHardCap) *
        (1f - MathF.Exp(-comp.OCurseRate * (totalSources - comp.ACurseLimit)));

        return MathF.Round(
            MathF.Min(lowCurse, highCurse),
            2);
    }
}