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
    public float TensionAcceleration { get; }
    public float BaseAsyncTimeStep { get; }
    public float TensionAccelerationDelta { get; }
    public float TensionAccelerationDeltaPressed { get; }
    public float ProgressPerTick { get; }
    public NetEntity? Bobber { get; }
    public bool Biting { get; }

    public FishingMinigameBoundUserInterfaceState(
        float tension,
        float progress,
        float tensionAcceleration,
        float baseAsyncTimeStep,
        float tensionAccelerationDelta,
        float tensionAccelerationDeltaPressed,
        float progressPerTick,
        NetEntity? bobber = null,
        bool biting = false)
    {
        Tension = tension;
        Progress = progress;
        TensionAcceleration = tensionAcceleration;
        BaseAsyncTimeStep = baseAsyncTimeStep;
        TensionAccelerationDelta = tensionAccelerationDelta;
        TensionAccelerationDeltaPressed = tensionAccelerationDeltaPressed;
        ProgressPerTick = progressPerTick;
        Bobber = bobber;
        Biting = biting;
    }
}

[Serializable, NetSerializable]
public enum FishingMinigameResult : byte
{
    Exit,
    Complete,
}

[Serializable, NetSerializable]
public sealed class FishingMinigameResultMessage : BoundUserInterfaceMessage
{
    public FishingMinigameResult Result { get; }

    public FishingMinigameResultMessage(FishingMinigameResult result)
    {
        Result = result;
    }
}

[Serializable, NetSerializable]
public sealed class FishingMinigameStopMessage : BoundUserInterfaceMessage;
