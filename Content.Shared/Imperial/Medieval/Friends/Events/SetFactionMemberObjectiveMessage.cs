using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

/// <summary>
/// Отправляется с клиента на сервер для назначения цели участнику фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SetFactionMemberObjectiveMessage : EntityEventArgs
{
    public NetEntity Ent;
    public string Objective;

    public SetFactionMemberObjectiveMessage(NetEntity ent, string objective)
    {
        Ent = ent;
        Objective = objective;
    }
}
