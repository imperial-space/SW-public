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
    public int Direction;
    public int Target { get; }

    // Конструктор для удобства создания события
    public RotateSailEvent(int direction, int uid)
    {
        Direction = direction;
        Target = uid;
    }
}



[Serializable, NetSerializable]
public sealed class OpenSailMenuEvent : EntityEventArgs
{
    public int User { get; }
    public int Target { get; }

    public OpenSailMenuEvent(int user, int target)
    {
        User = user;
        Target = target;
    }
    public OpenSailMenuEvent() { }
}

[Serializable, NetSerializable]
public sealed partial class RotateEvent : DoAfterEvent
{
    public bool Direction;

    public RotateEvent(bool dir)
    {
        Direction = dir;
    }

    public override DoAfterEvent Clone() => new RotateEvent(Direction);
}

