using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

[Serializable, NetSerializable]
public sealed partial class GPSTrackerRemoveDoAfterEvent : DoAfterEvent
{
    public GPSTrackerRemoveDoAfterEvent()
    {
    }
    public override DoAfterEvent Clone() => this;
}