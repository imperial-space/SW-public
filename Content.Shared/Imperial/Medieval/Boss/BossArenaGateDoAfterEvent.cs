using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public sealed partial class BossArenaGateDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Destination;

    [DataField]
    public uint Revision;

    [DataField]
    public bool Unlock;

    public BossArenaGateDoAfterEvent(NetEntity destination, uint revision, bool unlock)
    {
        Destination = destination;
        Revision = revision;
        Unlock = unlock;
    }

    public override DoAfterEvent Clone() => new BossArenaGateDoAfterEvent(Destination, Revision, Unlock);
}
