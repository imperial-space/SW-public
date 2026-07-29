using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[Serializable, NetSerializable]
public sealed partial class MedievalArmorRepairDoAfterEvent : DoAfterEvent
{
    [DataField]
    public float StationMaxArmorRemovalModifier { get; private set; } = 1f;

    [DataField]
    public float RepairDelayModifier { get; private set; } = 1f;

    private MedievalArmorRepairDoAfterEvent()
    {
    }

    public MedievalArmorRepairDoAfterEvent(
        float stationMaxArmorRemovalModifier,
        float repairDelayModifier)
    {
        StationMaxArmorRemovalModifier = stationMaxArmorRemovalModifier;
        RepairDelayModifier = repairDelayModifier;
    }

    public override DoAfterEvent Clone()
    {
        return new MedievalArmorRepairDoAfterEvent(
            StationMaxArmorRemovalModifier,
            RepairDelayModifier);
    }
}
