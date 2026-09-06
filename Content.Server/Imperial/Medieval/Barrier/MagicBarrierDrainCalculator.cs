using Content.Server.MagicBarrier.Components;

namespace Content.Server.MagicBarrier;

public static class MagicBarrierDrainCalculator
{
    public static float Calculate(MagicBarrierComponent comp, int growthCount, int riftCount)
    {
        var lowCurse =
            (comp.BaseCurseDrain + comp.RiftCurseDrain * riftCount) *
            MathF.Pow(
                comp.MagicBarrierCurseEffect,
                1f + growthCount);

        var totalSources = growthCount + riftCount;

        var highCurse = totalSources <= comp.ACurseLimit
            ? comp.HLCurseLimit
            : comp.HLCurseLimit +
              (comp.HHCurseLimit - comp.HLCurseLimit) *
              (1f - MathF.Exp(
                  -comp.OCurseRate *
                  (totalSources - comp.ACurseLimit)));

        return MathF.Round(
            MathF.Min(lowCurse, highCurse),
            2);
    }
}