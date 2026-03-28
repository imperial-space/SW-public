using Content.Shared.DoAfter;

using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Sail;


[Serializable, NetSerializable]
public sealed partial class SailUseEvent : SimpleDoAfterEvent
{
}

public sealed partial class SailFoldEvent : SimpleDoAfterEvent
{
}
