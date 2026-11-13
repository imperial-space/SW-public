using System;
using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

[Serializable, NetSerializable]
public enum AdvancedMeleeVisualState : byte
{
    None,
    Scheduled,
    Confirmed,
    Cancelled,
}

/// <summary>Presentation snapshot, never permission to attack or apply damage.</summary>
[Serializable, NetSerializable]
public sealed class AdvancedMeleeVisualData : IEquatable<AdvancedMeleeVisualData>
{
    public Vector2 WorldOffset;
    public Angle Angle;
    public string? Animation;
    public bool SwingLeft;
    public Angle SpriteRotation;
    public float Length;
    public float Offset;

    public bool Equals(AdvancedMeleeVisualData? other)
        => other != null && WorldOffset.Equals(other.WorldOffset) && Angle.Equals(other.Angle) &&
           Animation == other.Animation && SwingLeft == other.SwingLeft && SpriteRotation.Equals(other.SpriteRotation) &&
           Length.Equals(other.Length) && Offset.Equals(other.Offset);

    public override bool Equals(object? obj) => obj is AdvancedMeleeVisualData other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(WorldOffset, Angle, Animation, SwingLeft, SpriteRotation, Length, Offset);
}
