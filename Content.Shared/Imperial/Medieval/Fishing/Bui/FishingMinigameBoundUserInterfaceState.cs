using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Fishing.Bui;

[Serializable, NetSerializable]
public enum FishingRodUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class FishingMinigameBoundUserInterfaceState : BoundUserInterfaceState
{
    public float Tension { get; }
    public float Progress { get; }
    public NetEntity? Bobber { get; }
    public bool Biting { get; }

    public FishingMinigameBoundUserInterfaceState(float tension, float progress, NetEntity? bobber = null, bool biting = false)
    {
        Tension = tension;
        Progress = progress;
        Bobber = bobber;
        Biting = biting;
    }
}

[Serializable, NetSerializable]
public sealed class FishingMinigameInputMessage : BoundUserInterfaceMessage
{
    public bool HoldingLmb { get; }

    public FishingMinigameInputMessage(bool holdingLmb)
    {
        HoldingLmb = holdingLmb;
    }
}
