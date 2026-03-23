using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Cannon;

[Serializable, NetSerializable]
public sealed partial class CannonRamrodDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class CannonGunpowderDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class CannonLoadAmmoDoAfterEvent : SimpleDoAfterEvent
{
}
