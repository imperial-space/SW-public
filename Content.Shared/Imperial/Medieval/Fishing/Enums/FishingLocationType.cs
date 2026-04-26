using Robust.Shared.Serialization;

namespace Content.Shared.Fishing.Enums;

[Serializable, NetSerializable]
public enum FishingLocationType : byte
{
    River,
    Sea
}
