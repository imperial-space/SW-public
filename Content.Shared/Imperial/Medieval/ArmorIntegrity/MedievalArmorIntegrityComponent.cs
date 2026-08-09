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
    public Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> BrokenResistances = new()
    {
        { "Asphyxiation", new MedievalArmorResistance() },
        { "Bloodloss", new MedievalArmorResistance() },
        { "Blunt", new MedievalArmorResistance() },
        { "Cellular", new MedievalArmorResistance() },
        { "Caustic", new MedievalArmorResistance() },
        { "Cold", new MedievalArmorResistance() },
        { "Heat", new MedievalArmorResistance() },
        { "Piercing", new MedievalArmorResistance() },
        { "Poison", new MedievalArmorResistance() },
        { "Radiation", new MedievalArmorResistance() },
        { "Shock", new MedievalArmorResistance() },
        { "Slash", new MedievalArmorResistance() },
        { "Structural", new MedievalArmorResistance() },
        { "Holy", new MedievalArmorResistance() },
        { "ParryAble", new MedievalArmorResistance() },
    };

    [DataField, AutoNetworkedField]
    public MedievalArmorExplosionResistance UnbrokenExplosionResistance = new();

    [DataField, AutoNetworkedField]
    public MedievalArmorExplosionResistance BrokenExplosionResistance = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> BreakageMultipliers = new()
    {
        { "Piercing", 1f },
        { "Blunt", 1f },
        { "Slash", 1f },
        { "Heat", 1f },
    };

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
