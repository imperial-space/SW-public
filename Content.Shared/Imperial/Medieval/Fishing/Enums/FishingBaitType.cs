using Robust.Shared.Serialization;

namespace Content.Shared.Fishing.Enums;

[Serializable, NetSerializable]
public enum FishingBaitType : byte
{
    Meat,
    Plant
}
