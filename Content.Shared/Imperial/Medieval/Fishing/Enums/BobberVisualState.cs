using Robust.Shared.Serialization;

namespace Content.Shared.Fishing.Enums;

[Serializable, NetSerializable]
public enum BobberVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum BobberVisualState : byte
{
    Icon,
    Biting
}
