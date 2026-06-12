using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Power;

[Serializable, NetSerializable]
public enum MyrmexValveVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum MyrmexPowerVisuals : byte
{
    Powered,
}

[Serializable, NetSerializable]
public enum RandomPowerVisuals : byte
{
    Voltage,
}

[Serializable, NetSerializable]
public sealed partial class MyrmexValveDoAfterEvent : SimpleDoAfterEvent;
