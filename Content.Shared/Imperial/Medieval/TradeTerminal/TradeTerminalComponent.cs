using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Trade;

[Serializable, NetSerializable]
public enum TradeSessionState : byte
{
    Idle,
    Calling,
    Ringing,
    Active,
    Countdown,
    Completed,
}

[Serializable, NetSerializable]
public enum TradeTerminalVisuals : byte { State }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TradeTerminalComponent : Component
{
    [DataField]
    public string ContainerId = "storagebase";

    [AutoNetworkedField]
    public TradeSessionState State = TradeSessionState.Idle;

    [AutoNetworkedField]
    public EntityUid? LinkedTerminal;

    [AutoNetworkedField]
    public EntityUid? Owner;

    [DataField]
    public float CountdownDuration = 10f;

    [AutoNetworkedField]
    public TimeSpan CountdownEndTime;

    [AutoNetworkedField]
    public NetEntity? ConfirmedBy;

    [AutoNetworkedField]
    public bool HasConfirmed;

    public TimeSpan NextRingTime;
}
