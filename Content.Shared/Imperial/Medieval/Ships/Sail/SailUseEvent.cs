using Content.Shared.DoAfter;

using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Sail;


[Serializable, NetSerializable]
public sealed partial class SailUseEvent : SimpleDoAfterEvent
{
}
[Serializable, NetSerializable]
public sealed partial class SailFoldEvent : SimpleDoAfterEvent
{
}

// Событие для поворота паруса (клиент → сервер)
[Serializable, NetSerializable]
public sealed class RotateSailEvent : EntityEventArgs
{
    public int Direction; // Направление поворота (-1 или 1)
    public int IntUid; // Направление поворота (-1 или 1)

    // Конструктор для удобства создания события
    public RotateSailEvent(int direction, int uid)
    {
        Direction = direction;
        IntUid = uid;
    }
}



[Serializable, NetSerializable]
public sealed class OpenSailMenuEvent : EntityEventArgs
{
    public string MenuType { get; }
    public int SlotIndex { get; }

    public OpenSailMenuEvent(string menuType = "default", int slotIndex = 0)
    {
        MenuType = menuType;
        SlotIndex = slotIndex;
    }
}

[Serializable, NetSerializable]
public sealed partial class RotateEvent : DoAfterEvent
{
    public bool Direction;

    public RotateEvent(bool dir)
    {
        Direction = dir;
    }

public override DoAfterEvent Clone() => this;
}

