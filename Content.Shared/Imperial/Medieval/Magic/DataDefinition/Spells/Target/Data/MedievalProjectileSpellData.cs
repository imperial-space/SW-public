using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Magic;


/// <summary>
/// DoAfter parameters that are required to cast the spell
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class MedievalProjectileSpellData : MedievalTargetSpellData
{
    [DataField]
    public Dictionary<Angle, EntProtoId> ProjectilePrototype;

    [DataField]
    public float ProjectileSpeed = 20f;

    /// <summary>
    /// How far in front of the caster (towards the target) the projectile appears, in tiles.
    /// </summary>
    [DataField]
    public float SpawnForwardOffset = 0f;
}
