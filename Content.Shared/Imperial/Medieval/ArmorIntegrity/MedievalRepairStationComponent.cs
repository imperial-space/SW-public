namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[RegisterComponent]
public sealed partial class MedievalRepairStationComponent : Component
{
    [DataField]
    public MedievalArmorRepairType RepairType = MedievalArmorRepairType.Smithing;

    [DataField]
    public float StationMaxArmorRemovalModifier = 0.5f;

    [DataField]
    public float RepairDelayModifier = 0.5f;
}
