using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.CommsCharger;

[Serializable, NetSerializable]
public sealed partial class CommsChargerDoAfterEvent : SimpleDoAfterEvent;
