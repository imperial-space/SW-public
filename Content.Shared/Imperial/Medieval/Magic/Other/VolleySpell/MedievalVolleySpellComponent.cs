using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.VolleySpell;


/// <summary>
/// Placed on an entity target projectile spell action together with <c>MedievalTargetOverlayOnAiming</c>.
/// When the caster selects the action, the projectiles are summoned and hover behind the caster.
/// After the target is chosen, they are launched at it one after another.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalVolleySpellComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public int Count = 2;

    /// <summary>
    /// Width of the arc behind the caster over which the projectiles are spread, in degrees
    /// </summary>
    [DataField]
    public float Arc = 60f;

    [DataField]
    public float HoverRadius = 1.4f;

    /// <summary>
    /// Delay between the launches of two consecutive projectiles, in seconds
    /// </summary>
    [DataField]
    public float LaunchInterval = 0.15f;

    /// <summary>
    /// How long the projectiles wait for the target before they vanish, in seconds
    /// </summary>
    [DataField]
    public float HoverTimeout = 15f;

    [DataField]
    public SoundSpecifier? SummonSound;

    [DataField]
    public SoundSpecifier? LaunchSound;
}
