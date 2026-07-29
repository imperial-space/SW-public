using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[RegisterComponent]
public sealed partial class MedievalRepairArmorComponent : Component
{
    [DataField]
    public MedievalArmorRepairType RepairType = MedievalArmorRepairType.Smithing;

    [DataField]
    public float RepairAmount = 20f;

    [DataField]
    public bool IsSpendable;

    [DataField]
    public float RepairTime = 5f;

    [DataField]
    public float RepairStationSearchRange = 0.5f;

    [DataField]
    public int BaselineIntelligence = 10;

    [DataField]
    public float MinimumRepairDelay = 0.01f;

    [DataField]
    public float MaxArmorRemove;

    [DataField]
    public float SkilledCrafterMaxArmorRemovalModifier = 0.5f;

    [DataField]
    public SoundSpecifier? UseSound;
}
