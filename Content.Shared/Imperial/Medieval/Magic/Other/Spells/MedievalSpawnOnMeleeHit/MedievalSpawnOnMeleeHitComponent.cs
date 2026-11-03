using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalSpawnOnMeleeHit;


/// <summary>
/// Spawns an effect on every entity hit by this melee weapon
/// </summary>
[RegisterComponent]
public sealed partial class MedievalSpawnOnMeleeHitComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}
