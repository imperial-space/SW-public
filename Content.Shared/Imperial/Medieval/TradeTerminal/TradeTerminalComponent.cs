using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Maths;

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

    [DataField]
    public float CompletedCleanupDelay = 120f;

    [DataField]
    public int OfferGridWidth = 8;

    [DataField]
    public int OfferGridHeight = 5;

    [AutoNetworkedField]
    public TimeSpan CountdownEndTime;

    [AutoNetworkedField]
    public NetEntity? ConfirmedBy;

    [AutoNetworkedField]
    public bool HasConfirmed;

    public TimeSpan NextRingTime;
    public TimeSpan CallTimeoutTime;
    public TimeSpan CompletedExpireTime;

    [NonSerialized]
    public Dictionary<EntityUid, Vector2i> OfferSlots = new();

    [NonSerialized]
    public Dictionary<EntityUid, Vector2i> PendingOfferSlots = new();
}
