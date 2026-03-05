using Robust.Shared.Serialization;

namespace Content.Shared.Trade;

[Serializable, NetSerializable]
public enum TradeUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class TradeCallMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;
    public TradeCallMessage(NetEntity target) => Target = target;
}

[Serializable, NetSerializable]
public sealed class TradeAcceptCallMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class TradeHangUpMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class TradeRemoveItemMessage : BoundUserInterfaceMessage
{
    public NetEntity Item;
    public TradeRemoveItemMessage(NetEntity item) => Item = item;
}

[Serializable, NetSerializable]
public sealed class TradeConfirmMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class TradeCancelMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class TradeItemDto
{
    public NetEntity Entity;
    public string    Name;
    public string?   Description;
    public int?      StackCount;

    public TradeItemDto(NetEntity entity, string name, string? description = null, int? stackCount = null)
    {
        Entity      = entity;
        Name        = name;
        Description = description;
        StackCount  = stackCount;
    }
}

[Serializable, NetSerializable]
public sealed class TradeTerminalDto
{
    public NetEntity         Entity;
    public string            Name;
    public TradeSessionState State;

    public TradeTerminalDto(NetEntity entity, string name, TradeSessionState state)
    {
        Entity = entity;
        Name   = name;
        State  = state;
    }
}

[Serializable, NetSerializable]
public sealed class TradeBuiState : BoundUserInterfaceState
{
    public TradeSessionState  OwnState;
    public string             OwnName;
    public List<TradeItemDto> OwnItems;

    public string?              PartnerName;
    public TradeSessionState?   PartnerState;
    public List<TradeItemDto>?  PartnerItems;

    public string?  IncomingCallerName;
    public TimeSpan CountdownEndTime;
    public float    CountdownDuration;
    public string?  ConfirmedByName;

    public List<TradeTerminalDto> Directory;
    public bool OwnConfirmed;
    public bool PartnerConfirmed;

    public TradeBuiState(
        TradeSessionState ownState,
        string ownName,
        List<TradeItemDto> ownItems,
        string? partnerName,
        TradeSessionState? partnerState,
        List<TradeItemDto>? partnerItems,
        string? incomingCallerName,
        TimeSpan countdownEndTime,
        float countdownDuration,
        string? confirmedByName,
        List<TradeTerminalDto> directory,
        bool ownConfirmed,
        bool partnerConfirmed)
    {
        OwnState           = ownState;
        OwnName            = ownName;
        OwnItems           = ownItems;
        PartnerName        = partnerName;
        PartnerState       = partnerState;
        PartnerItems       = partnerItems;
        IncomingCallerName = incomingCallerName;
        CountdownEndTime   = countdownEndTime;
        CountdownDuration  = countdownDuration;
        ConfirmedByName    = confirmedByName;
        Directory          = directory;

        OwnConfirmed       = ownConfirmed;
        PartnerConfirmed   = partnerConfirmed;
    }
}

public sealed class TradeExecutedEvent : EntityEventArgs
{
    public EntityUid TerminalA;
    public EntityUid TerminalB;

    public TradeExecutedEvent(EntityUid a, EntityUid b)
    {
        TerminalA = a;
        TerminalB = b;
    }
}

public sealed class TradeCancelledEvent : EntityEventArgs
{
    public EntityUid  Terminal;
    public EntityUid? CancelledBy;

    public TradeCancelledEvent(EntityUid terminal, EntityUid? cancelledBy = null)
    {
        Terminal    = terminal;
        CancelledBy = cancelledBy;
    }
}
