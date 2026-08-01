using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Flagpole;

[Serializable, NetSerializable]
public sealed class MedievalShipFlagpoleOpenMessage : EntityEventArgs
{
    public NetEntity Flagpole { get; }

    public MedievalShipFlagpoleOpenMessage(NetEntity megaphone)
    {
        Flagpole = megaphone;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalShipFlagpoleSelectedMessage : BoundUserInterfaceMessage
{
    public MedievalShipFlagpoleMenuAction FlagColor;

    public MedievalShipFlagpoleSelectedMessage(MedievalShipFlagpoleMenuAction flagColor)
    {
        FlagColor = flagColor;
    }
}

[Serializable, NetSerializable]
public enum MedievalShipFlagpoleUiKey
{
    Key
}

[Serializable, NetSerializable]
public enum MedievalShipFlagpoleMenuAction : byte
{
    Black,
    Red,
    White,
    Brown,
    Cyan,
    DarkRed,
    Gray,
    Green,
    Orange,
    Pink,
    Purple,
    Yellow,
    Blue,
    Pirate,
    Legion,
    Insurgency,
    Collegium,
    Mercenary,
    None
}

[Serializable, NetSerializable]
public enum MedievalShipFlagpoleVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public sealed partial class MedievalShipFlagpoleDoAfterEvent : DoAfterEvent
{
    public MedievalShipFlagpoleMenuAction Action;
    public MedievalShipFlagpoleDoAfterEvent(MedievalShipFlagpoleMenuAction action)
    {
        Action = action;
    }

    public override DoAfterEvent Clone() => new MedievalShipFlagpoleDoAfterEvent(Action);
}
