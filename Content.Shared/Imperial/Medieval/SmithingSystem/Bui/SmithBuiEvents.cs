using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Bui;

[Serializable, NetSerializable]
public enum SmithHitState
{
    Good,
    Neutral,
    Missed
}

[Serializable, NetSerializable]
public sealed class StartSmithGameEvent : BoundUserInterfaceState
{
    public SmithGameSettings GameSettings { get; }

    public StartSmithGameEvent(SmithGameSettings gameSettings)
    {
        GameSettings = gameSettings;
    }
}

[Serializable, NetSerializable]
public sealed class SmithGameSettings
{
    public int Steps;
    public double GoldTime;
    public double NothingTime;
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

