using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossAutoProjectileVisuals
{
    public const double ChargeBeginSeconds = 0.12;
    public const double LaunchDelaySeconds = 0.34;
    public const double FlightFrameSeconds = 0.10;
    public const double FlightLoopSeconds = 1.20;
    public const double ImpactSeconds = 0.50;
    public const double MaxFlightSeconds = 10;
    public static TimeSpan FlameTime(double age) => TimeSpan.FromSeconds(double.IsFinite(age) ? Math.Max(0, age - ChargeBeginSeconds) : 0);
    public static float FormationProgress(double age) => Smooth((age - ChargeBeginSeconds) / (LaunchDelaySeconds - ChargeBeginSeconds));
    public static float BirthOpacity(double age) => Smooth((age - ChargeBeginSeconds) / .04);
    private static float Smooth(double t)
    {
        if (!double.IsFinite(t))
            return 0;
        var p = (float)Math.Clamp(t, 0, 1);
        return p * p * (3 - 2 * p);
    }

    public static float ImpactOpacity(double age) => !double.IsFinite(age) || age < 0 || age >= ImpactSeconds ? 0 : age < 0.10 ? 1 : (float)Math.Pow(1 - age / ImpactSeconds, 0.7);
}
