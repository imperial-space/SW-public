using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

/// <summary>
/// Отправляется с клиента на сервер для назначения группы участнику фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SetFactionMemberGroupMessage : EntityEventArgs
{
    public NetEntity Ent;
    public string Group;

    public SetFactionMemberGroupMessage(NetEntity ent, string group)
    {
        Ent = ent;
        Group = group;
    }
}
