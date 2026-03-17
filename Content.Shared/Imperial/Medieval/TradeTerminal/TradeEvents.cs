using Content.Shared.Storage;
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
public sealed class TradeInsertHeldItemAtMessage : BoundUserInterfaceMessage
{
    public ItemStorageLocation Location;
    public int Amount;

    public TradeInsertHeldItemAtMessage(ItemStorageLocation location, int amount = 0)
    {
        Location = location;
        Amount = amount;
    }
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
    public ItemStorageLocation StorageLocation;
    public int GridWidth;
    public int GridHeight;

    public TradeItemDto(
        NetEntity entity,
        string name,
        string? description = null,
        int? stackCount = null,
        ItemStorageLocation storageLocation = default,
        int gridWidth = 1,
        int gridHeight = 1)
    {
        Entity      = entity;
        Name        = name;
        Description = description;
        StackCount  = stackCount;
        StorageLocation = storageLocation;
        GridWidth = gridWidth;
        GridHeight = gridHeight;
    }
}

[Serializable, NetSerializable]
public sealed class TradeOfferGridDto
{
    public int Width;
    public int Height;

    public TradeOfferGridDto(int width, int height)
    {
        Width = width;
        Height = height;
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
    public TradeOfferGridDto  OwnGrid;

    public string?              PartnerName;
    public TradeSessionState?   PartnerState;
    public List<TradeItemDto>?  PartnerItems;
    public TradeOfferGridDto    PartnerGrid;

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
        TradeOfferGridDto ownGrid,
        string? partnerName,
        TradeSessionState? partnerState,
        List<TradeItemDto>? partnerItems,
        TradeOfferGridDto partnerGrid,
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
        OwnGrid            = ownGrid;
        PartnerName        = partnerName;
        PartnerState       = partnerState;
        PartnerItems       = partnerItems;
        PartnerGrid        = partnerGrid;
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
