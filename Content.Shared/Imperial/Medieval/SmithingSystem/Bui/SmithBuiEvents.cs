using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Bui;

[Serializable, NetSerializable]
public enum SmithHitState
{
    Good,
    Neutral,
    Missed,
    Penalty
}


[Serializable, NetSerializable]
public sealed class SmithHitMesage : BoundUserInterfaceMessage
{
    public SmithHitState State { get; }
    public bool Increment { get; }

    public SmithHitMesage(SmithHitState state, bool increment)
    {
        State = state;
        Increment = increment;
    }
}

[Serializable, NetSerializable]
public sealed class ClientStartedGameEvent : BoundUserInterfaceMessage;

