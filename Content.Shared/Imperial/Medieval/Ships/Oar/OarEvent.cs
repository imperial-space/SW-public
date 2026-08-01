using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Oar;

[Serializable, NetSerializable]
public sealed partial class OnOarDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed class OarUseRequestEvent : EntityEventArgs
{
    public readonly NetEntity Oar;
    public readonly NetCoordinates ClickCoordinates;

    public OarUseRequestEvent(NetEntity oar, NetCoordinates clickCoordinates)
    {
        Oar = oar;
        ClickCoordinates = clickCoordinates;
    }
}
