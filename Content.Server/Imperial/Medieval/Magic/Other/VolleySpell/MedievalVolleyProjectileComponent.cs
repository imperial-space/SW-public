using System.Numerics;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Magic.VolleySpell;


/// <summary>
/// A projectile of a volley spell that hovers behind the caster and waits for its launch
/// </summary>
[RegisterComponent]
public sealed partial class MedievalVolleyProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid Caster;

    [ViewVariables]
    public EntityUid Action;

    [ViewVariables]
    public int Order;

    /// <summary>
    /// Place in the arc behind the caster, relative to the direction the caster is facing away from
    /// </summary>
    [ViewVariables]
    public Angle Slot;

    [ViewVariables]
    public float HoverRadius;

    /// <summary>
    /// Current distance from the caster. It is shorter than <see cref="HoverRadius"/> while a wall is in the way.
    /// </summary>
    [ViewVariables]
    public float Radius;

    /// <summary>
    /// Direction from the caster to the hovering projectile, smoothed so that the formation follows turns gently
    /// </summary>
    [ViewVariables]
    public Vector2 Back;

    /// <summary>
    /// Smoothed shift along the caster's velocity, see VelocityLead in the system
    /// </summary>
    [ViewVariables]
    public Vector2 Lead;

    [ViewVariables]
    public float Phase;

    [ViewVariables]
    public Angle Rotation;

    [ViewVariables]
    public TimeSpan Deadline;

    /// <summary>
    /// When set, the projectile is removed at this time unless it is launched before
    /// </summary>
    [ViewVariables]
    public TimeSpan? DismissTime;

    /// <summary>
    /// Null while the projectile is waiting for the target
    /// </summary>
    [ViewVariables]
    public TimeSpan? LaunchTime;

    [ViewVariables]
    public bool Settling;

    [ViewVariables]
    public Angle SettleFrom;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public Vector2 TargetPosition;

    [ViewVariables]
    public float Speed;

    [ViewVariables]
    public SoundSpecifier? LaunchSound;
}
