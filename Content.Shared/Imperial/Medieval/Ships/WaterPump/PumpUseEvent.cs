using Content.Shared.DoAfter;

using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;


[Serializable, NetSerializable]
public sealed partial class PumpUseEvent : SimpleDoAfterEvent
{
}
