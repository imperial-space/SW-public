using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossAutoProjectileComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public SpriteSpecifier.Rsi Flight = default!;
    [DataField(required: true), AutoNetworkedField]
    public SpriteSpecifier.Rsi Charge = default!;
    [DataField(required: true), AutoNetworkedField]
    public SpriteSpecifier.Rsi Trail = default!;
    [DataField(required: true), AutoNetworkedField]
    public SpriteSpecifier.Rsi Impact = default!;
    [DataField, AutoNetworkedField]
    public float Scale = 0.5f;
    [DataField, AutoNetworkedField]
    public Vector2 AnchorPixels = new(120, 80);                                  // sprite pivot in pixels
    [DataField, AutoNetworkedField]
    public float Speed = 10f;                                                    // travel speed in tiles per second
    [DataField, AutoNetworkedField]
    public Vector2 HandOffset = new(0, 0.5f);                                    // launch offset from the boss in grid units
    public uint Generation;
    [AutoNetworkedField]
    public EntityUid Boss;
    [AutoNetworkedField]
    public EntityUid Target;
    [AutoNetworkedField]
    public EntityUid Grid;
    [AutoNetworkedField]
    public Vector2 Position;
    [AutoNetworkedField]
    public Vector2 Direction = Vector2.UnitX;
    [AutoNetworkedField]
    public bool Resolved;
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LaunchTime;                                                  // charging ends and movement starts
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan PositionTime;
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ImpactTime;
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;
    public SpriteSpecifier.Rsi Animation(SpriteSpecifier.Rsi state) => new(SpriteSpecifierSerializer.TextureRoot / state.RsiPath, state.RsiState);
    public Vector2 AimDirection(Vector2 target)
    {
        var delta = target - Position;
        return delta.LengthSquared() > 0.000001f ? Vector2.Normalize(delta) : Direction;
    }

    public Vector2 PredictedPosition(TimeSpan now, Vector2 target)
    {
        if (Resolved || now < LaunchTime)
            return Position;
        var from = PositionTime > LaunchTime ? PositionTime : LaunchTime;
        var elapsed = (float)Math.Clamp((now - from).TotalSeconds, 0, 0.2);
        var delta = target - Position;
        var distance = delta.Length();
        return distance <= Speed * elapsed || distance < 0.0001f ? target : Position + delta / distance * Speed * elapsed;
    }
}
