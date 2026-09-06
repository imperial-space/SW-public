using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Magic.ActionOrder;

[Serializable, NetSerializable]
public sealed class MedievalActionReplacedEvent(NetEntity oldAction, NetEntity newAction) : EntityEventArgs
{
    public NetEntity OldAction = oldAction;
    public NetEntity NewAction = newAction;
}
