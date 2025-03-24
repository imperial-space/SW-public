using Content.Shared.Friends.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

/// <summary>
/// Отправляется с клиента на сервер для назначения цели участнику фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SetFactionMemberObjectiveMessage : EntityEventArgs
{
    public ProtoId<MedievalFactionPrototype> Faction;
    public FactionMemberGroup Group;
    public string Objective;

    public SetFactionMemberObjectiveMessage(ProtoId<MedievalFactionPrototype> faction, FactionMemberGroup group, string objective)
    {
        Faction = faction;
        Group = group;
        Objective = objective;
    }
}
