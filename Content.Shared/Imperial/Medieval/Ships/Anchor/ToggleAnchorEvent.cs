using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

[Serializable, NetSerializable]
public sealed partial class ToggleAnchorEvent : SimpleDoAfterEvent
{
}
