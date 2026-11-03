using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalRain;


/// <summary>
/// Falls for <see cref="FallTime" />, then damages everyone alive around the landing point
/// </summary>
[RegisterComponent]
public sealed partial class MedievalRainDropComponent : Component
{
    /// <summary>
    /// Time in seconds between the appearance and the landing
    /// </summary>
    [DataField]
    public float FallTime = 0.4f;

    /// <summary>
    /// Radius of the damage around the landing point
    /// </summary>
    [DataField]
    public float Radius = 0.8f;

    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Damage that ignores the armor of the target
    /// </summary>
    [DataField]
    public DamageSpecifier ApDamage = new();

    /// <summary>
    /// Effect spawned at the landing point
    /// </summary>
    [DataField]
    public EntProtoId? Impact;

    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables]
    public TimeSpan LandAt;

    [ViewVariables]
    public EntityUid Performer;
}
