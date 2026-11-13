namespace Content.Shared.Imperial.Medieval.Poise.CombatStamina;

/// <summary>Accounting in actual stamina units, after action/damage modifiers.</summary>
public static class CombatStaminaMath
{
    public const float Epsilon = 0.00001f;

    public static (float CombatSpent, float NormalCost) SplitCost(float pending, float cost, float multiplier)
    {
        if (!float.IsFinite(pending) || !float.IsFinite(cost) || !float.IsFinite(multiplier) ||
            pending <= 0f || cost <= 0f || multiplier <= 0f)
            return (0f, MathF.Max(0f, float.IsFinite(cost) ? cost : 0f));

        var covered = MathF.Min(cost, pending / multiplier);
        return (covered * multiplier, MathF.Max(0f, cost - covered));
    }

    public static float RecoveryAmount(float pending, float rate, TimeSpan previous, TimeSpan starts, TimeSpan now)
    {
        if (!float.IsFinite(pending) || !float.IsFinite(rate) || pending <= 0f || rate <= 0f)
            return 0f;
        var from = previous > starts ? previous : starts;
        var elapsed = Math.Max(0.0, (now - from).TotalSeconds);
        return (float) Math.Min(pending, elapsed * rate);
    }
}
