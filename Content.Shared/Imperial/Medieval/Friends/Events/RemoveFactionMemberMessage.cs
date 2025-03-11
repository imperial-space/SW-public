using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

/// <summary>
/// Отправляется с клиента на сервер при подтверждении удаления участника фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveFactionMemberMessage : EntityEventArgs
{
    public NetEntity Ent;

    public RemoveFactionMemberMessage(NetEntity ent)
    {
        Ent = ent;
    }
}
