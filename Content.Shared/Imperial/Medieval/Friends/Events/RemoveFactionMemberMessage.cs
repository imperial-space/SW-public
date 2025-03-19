using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

/// <summary>
/// Отправляется с клиента на сервер при подтверждении удаления участника фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveFactionMemberMessage : EntityEventArgs
{
    public int Ent;
    public int Performer;
    public bool Headhunt;

    public RemoveFactionMemberMessage(int ent, int performer, bool headhunt)
    {
        Ent = ent;
        Performer = performer;
        Headhunt = headhunt;
    }
}
