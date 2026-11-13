using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Magic;


/// <summary>
/// Called to cast a spell with a projectile
/// </summary>
public sealed partial class MedievalProjectileSpellEvent : MedievalTargetSpellEvent
{
    /// <summary>
    /// What entity should be spawned.
    /// </summary>
    [DataField]
    public Dictionary<Angle, EntProtoId> ProjectilePrototype = new();

    /// <summary>
    /// Start projectile speed
    /// </summary>
    [DataField]
    public float ProjectileSpeed = 20f;

    /// <summary>
    /// How far in front of the caster (towards the target) the projectile appears, in tiles.
    /// </summary>
    [DataField]
    public float SpawnForwardOffset = 0f;

    /// <summary>
    /// The caster keeps turned towards the target point until the cast ends.
    /// </summary>
    [DataField]
    public bool FaceTargetWhileCasting = false;
}
