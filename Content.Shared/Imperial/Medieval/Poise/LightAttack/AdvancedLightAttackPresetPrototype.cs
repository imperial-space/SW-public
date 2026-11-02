using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>Reusable stamina and visible wind-up settings.</summary>
[Prototype("advancedLightAttackPreset")]
public sealed partial class AdvancedLightAttackPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float StaminaCost = 5f;

    [DataField]
    public float Windup = 0.4f;

    // Arc width in degrees, independent from charged melee weapon Angle.
    [DataField]
    public float RaycastArc = 30f;

    // preparation is separate from this visible minimum.
    [DataField]
    public float ChargeWindup = 0.6f;

    [DataField]
    public EntProtoId WindupEffect = "MedievalAdvancedLightAttackCue";
}
