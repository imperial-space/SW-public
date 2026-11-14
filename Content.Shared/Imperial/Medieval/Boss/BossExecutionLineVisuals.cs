using System.Numerics;
using Robust.Shared.Graphics.RSI;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossExecutionLineVisuals
{
    public const float Width = 3f;
    public const float EdgeExtension = 0.5f;
    public const float Duration = 1.2f;
    public const float FrameDuration = 0.04f;
    public const float BurstTravelSeconds = 0.12f;
    public const float EyesFrameDuration = 0.24f;
    public const float WarningFrameDuration = 0.32f;
    public const float WarningOpacity = 0.42f;
    public static readonly Vector2 EyesOffset = new(0, 2f);
    public static readonly Vector2 EyesAnchorPixels = new(32, 38);
    public static Box2 EyesBounds(Vector2i frameSize)
        => Box2.FromDimensions(new Vector2(-EyesAnchorPixels.X, EyesAnchorPixels.Y - frameSize.Y) / 32,
            new Vector2(frameSize.X, frameSize.Y) / 32);

    public static float VisibleLength(float length, double age)
        => length * (float)Math.Clamp((age + FrameDuration) / BurstTravelSeconds, 0, 1);

    public static RsiDirection TipDirection(float angle)
    {
        if (!float.IsFinite(angle)) return RsiDirection.East;

        angle = MathF.IEEERemainder(angle, MathF.Tau);
        var octant = (int)MathF.Floor(angle / (MathF.PI / 4) + 0.5f);
        return ((octant % 8 + 8) % 8) switch
        {
            0 => RsiDirection.East,
            1 => RsiDirection.NorthEast,
            2 => RsiDirection.North,
            3 => RsiDirection.NorthWest,
            4 => RsiDirection.West,
            5 => RsiDirection.SouthWest,
            6 => RsiDirection.South,
            _ => RsiDirection.SouthEast,
        };
    }

    public static (RsiDirection Facing, float Rotation) TipPose(Vector2 worldDirection, float eyeRotation)
    {
        var direction = Aim(Vector2.Zero, worldDirection, Vector2.UnitX);
        var worldAngle = MathF.Atan2(direction.Y, direction.X);
        var viewRotation = float.IsFinite(eyeRotation) ? MathF.IEEERemainder(eyeRotation, MathF.Tau) : 0;
        var facing = TipDirection(worldAngle + viewRotation);

        return (facing, TipResidualAngle(worldAngle, facing));
    }

    public static float TipAngle(RsiDirection direction) => direction switch
    {
        RsiDirection.North => MathF.PI / 2,
        RsiDirection.NorthEast => MathF.PI / 4,
        RsiDirection.NorthWest => 3 * MathF.PI / 4,
        RsiDirection.West => MathF.PI,
        RsiDirection.SouthWest => -3 * MathF.PI / 4,
        RsiDirection.South => -MathF.PI / 2,
        RsiDirection.SouthEast => -MathF.PI / 4,
        _ => 0,
    };

    public static (RsiDirection TextureDirection, bool MirrorX) TipProfile(RsiDirection facing) => facing switch
    {
        RsiDirection.West => (RsiDirection.East, true),
        RsiDirection.NorthWest => (RsiDirection.NorthEast, true),
        RsiDirection.SouthWest => (RsiDirection.SouthEast, true),
        _ => (facing, false),
    };

    public static Vector2 TipAnchorPixels(RsiDirection direction) => direction switch
    {
        RsiDirection.North => new(32, 7),
        RsiDirection.NorthEast => new(49, 15),
        RsiDirection.East => new(56, 32),
        RsiDirection.SouthEast => new(48, 49),
        RsiDirection.South => new(31, 56),
        RsiDirection.SouthWest => new(14, 48),
        RsiDirection.West => new(7, 31),
        _ => new(15, 14),
    };

    public static Vector2 TipAnchorPixels(RsiDirection direction, Vector2i frameSize)
        => frameSize == new Vector2i(128, 128) ? direction switch
        {
            RsiDirection.South => new(63, 120),
            RsiDirection.North => new(64, 7),
            RsiDirection.East => new(120, 64),
            RsiDirection.West => new(7, 63),
            RsiDirection.SouthEast => new(99, 100),
            RsiDirection.SouthWest => new(27, 99),
            RsiDirection.NorthEast => new(100, 28),
            _ => new(28, 27),
        } : frameSize == new Vector2i(96, 96) ? direction switch
        {
            RsiDirection.North => new(48, 7),
            RsiDirection.NorthEast => new(84, 12),
            RsiDirection.East => new(88, 48),
            RsiDirection.SouthEast => new(83, 84),
            RsiDirection.South => new(47, 88),
            RsiDirection.SouthWest => new(11, 83),
            RsiDirection.West => new(7, 47),
            _ => new(12, 11),
        } : TipAnchorPixels(direction);

    public static float TipResidualAngle(float angle, RsiDirection direction)
    {
        var delta = angle - TipAngle(direction);
        return MathF.Atan2(MathF.Sin(delta), MathF.Cos(delta));
    }

    public static Box2 TipBounds(RsiDirection direction, Vector2i frameSize)
    {
        var anchor = TipAnchorPixels(direction, frameSize);
        return Box2.FromDimensions(new Vector2(-anchor.X, anchor.Y - frameSize.Y) / 32,
            new Vector2(frameSize.X, frameSize.Y) / 32);
    }

    public static Vector2 Aim(Vector2 origin, Vector2 target, Vector2 previous)
    {
        var delta = target - origin;
        if (float.IsFinite(delta.X) && float.IsFinite(delta.Y) && delta.LengthSquared() > 0.0001f)
            return Vector2.Normalize(delta);
        return float.IsFinite(previous.X) && float.IsFinite(previous.Y) && previous.LengthSquared() > 0.0001f
            ? Vector2.Normalize(previous) : Vector2.UnitX;
    }

    public static float LengthToEdge(Vector2 origin, Vector2 direction, int halfSize)
    {
        var min = -halfSize;
        var max = halfSize + 1;
        var x = MathF.Abs(direction.X) < 0.0001f ? float.PositiveInfinity :
            ((direction.X > 0 ? max : min) - origin.X) / direction.X;
        var y = MathF.Abs(direction.Y) < 0.0001f ? float.PositiveInfinity :
            ((direction.Y > 0 ? max : min) - origin.Y) / direction.Y;
        return MathF.Max(0, MathF.Min(x, y)) + EdgeExtension;
    }

    public static bool Contains(Vector2 point, Vector2 origin, Vector2 direction, float length, float width)
    {
        if (!float.IsFinite(width) || width <= 0 || !float.IsFinite(length) || length <= 0) return false;
        var relative = point - origin;
        var along = Vector2.Dot(relative, direction);
        var across = MathF.Abs(relative.X * direction.Y - relative.Y * direction.X);
        return along >= 0 && along <= length && across <= width / 2;
    }

    public static float Fade(double age)
    {
        var t = (float)Math.Clamp((age - 0.24) / (Duration - 0.24), 0, 1);
        return age < 0 ? 0 : 1 - t * t * (3 - 2 * t);
    }
}
