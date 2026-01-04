using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Skills;

public static class ThrowSpeedHelper
{
    public readonly record struct ThrowKinematics(float ThrowSpeed, float FlyTimeSeconds);

    public static ThrowKinematics Compute(
        Vector2 direction,
        float baseThrowSpeed,
        float tileFriction,
        bool compensateFriction,
        float flyTimePercentage,
        EntityUid thrownItem,
        EntityUid? user,
        IEntityManager entManager)
    {
        // Guards
        if (baseThrowSpeed <= 0f || direction == Vector2.Zero || !IsFinite(direction))
            return new ThrowKinematics(0f, 0f);

        if (!IsFinite(tileFriction) || tileFriction <= 0f)
            compensateFriction = false;

        if (!IsFinite(flyTimePercentage) || flyTimePercentage <= 0f)
            flyTimePercentage = 1f;

        // Skill multiplier
        var mult = 1f;
        if (user is { } thrower)
        {
            var ev = new GetThrowSpeedMultiplierEvent(thrownItem, thrower, 1f);
            entManager.EventBus.RaiseLocalEvent(thrower, ref ev);
            mult = SanitizeMultiplier(ev.Multiplier);
        }

        var speedBase = baseThrowSpeed * mult;
        if (speedBase <= 0f || !IsFinite(speedBase))
            return new ThrowKinematics(0f, 0f);

        var distance = direction.Length();
        var flyTime = distance / speedBase;

        if (!compensateFriction)
            return new ThrowKinematics(speedBase, flyTime);

        var adjustedFlyTime = flyTime * flyTimePercentage;
        var throwSpeed = distance / (adjustedFlyTime + 1f / tileFriction);

        return new ThrowKinematics(throwSpeed, adjustedFlyTime);
    }

    private static float SanitizeMultiplier(float mult)
        => (IsFinite(mult) && mult > 0f) ? mult : 1f;

    private static bool IsFinite(float v)
        => !float.IsNaN(v) && !float.IsInfinity(v);

    private static bool IsFinite(Vector2 v)
        => IsFinite(v.X) && IsFinite(v.Y);
}
