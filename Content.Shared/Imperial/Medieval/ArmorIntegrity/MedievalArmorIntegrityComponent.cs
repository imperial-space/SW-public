using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(MedievalArmorIntegritySystem))]
public sealed partial class MedievalArmorIntegrityComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> UnbrokenResistances = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> BrokenResistances = new();

    [DataField, AutoNetworkedField]
    public MedievalArmorExplosionResistance UnbrokenExplosionResistance = new();

    [DataField, AutoNetworkedField]
    public MedievalArmorExplosionResistance BrokenExplosionResistance = new();

    [DataField]
    public List<float> QualityMultipliers = new()
    {
        0.5f,
        0.5f,
        0.75f,
        1f,
        1.125f,
        1.25f,
    };

    [DataField]
    public bool QualityMultiplierApplied;

    [DataField, AutoNetworkedField]
    public bool IsBroken;

    [DataField, AutoNetworkedField]
    public MedievalArmorRepairType RepairType = MedievalArmorRepairType.Smithing;

    [DataField]
    public List<EntProtoId> ArmorBrokenEffects = new()
    {
        "MedievalArmorCrushEffect1",
        "MedievalArmorCrushEffect2",
        "MedievalArmorCrushEffect3",
    };

    [DataField]
    public SoundSpecifier BreakSound = new SoundPathSpecifier(
        "/Audio/Imperial/Medieval/Effects/integrity_break_metal.ogg");

    [DataField, AutoNetworkedField]
    public float ContainerArmorHP = 100f;

    [DataField, AutoNetworkedField]
    public float MaxArmorHP = 100f;

    [DataField, AutoNetworkedField]
    public float CurrentArmorHP = 100f;

    /// New settings
    public const float BrokenDefenseRetention = 0.25f;
    public const float BrokenRepairThreshold = 0.5f;
    public const float MinimumMaxArmorPercent = 0.25f;

    public const float WeakPercentMargin = 0.25f;
    public const float WeakHpMultiplier = 2f;
    public const float FocusDamageThreshold = 5f;

    public const float OuterWeight = 4f;
    public const float HeadWeight = 2f;
    public const float LegsWeight = 2f;
    public const float GlovesWeight = 1f;
    public const float FeetWeight = 1f;
    public const float DefaultWeight = 1f;

    public const float HighDamageMultiplier = 0.5f;

    public static readonly (float Threshold, float ArmorDamage)[] DamageCurve =
    [
        (2f, 0f),
        (5f, 2.5f),
        (10f, 5f),
        (15f, 7.5f),
        (20f, 10f),
        (25f, 12.5f),
        (30f, 15f),
        (35f, 17.5f),
        (40f, 20f),
        (45f, 22.5f),
        (50f, 25f),
    ];
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MedievalArmorResistance
{
    [DataField]
    public float Coefficient = 1f;

    [DataField]
    public float FlatReduction;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MedievalArmorExplosionResistance
{
    [DataField]
    public float DamageCoefficient = 1f;

    [DataField]
    public bool Worn = true;

    [DataField]
    public LocId Examine = "explosion-resistance-coefficient-value";

    [DataField("modifiers", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<float, ExplosionPrototype>))]
    public Dictionary<string, float> Modifiers = new();
}
