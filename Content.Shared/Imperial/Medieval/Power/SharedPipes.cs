using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Power;

[Serializable, NetSerializable]
public enum MyrmexValveVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public sealed partial class MyrmexValveDoAfterEvent : SimpleDoAfterEvent;
