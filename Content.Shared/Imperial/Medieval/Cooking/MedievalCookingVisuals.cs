using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Cooking;

[Serializable, NetSerializable]
public enum MedievalHearthVisuals : byte
{
    State,
    Logs
}

[Serializable, NetSerializable]
public enum MedievalHearthState : byte
{
    Out,
    Dying,
    Low,
    Burning
}

[Serializable, NetSerializable]
public enum MedievalPotVisuals : byte
{
    State,
    // read by the icons floating above the pot
    Result,
    Portions,
    Water,
    MaxWater
}

[Serializable, NetSerializable]
public enum MedievalPotState : byte
{
    Empty,
    Idle,
    Cooking,
    Ready,
    Burnt
}
