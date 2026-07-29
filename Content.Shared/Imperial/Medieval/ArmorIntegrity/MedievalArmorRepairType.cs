using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[Serializable, NetSerializable]
public enum MedievalArmorRepairType : byte
{
    Smithing,
    Sewing,
}
