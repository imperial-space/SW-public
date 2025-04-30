using System.Diagnostics.CodeAnalysis;
using Content.Shared.Examine;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;

namespace Content.Shared.Friends;
public abstract partial class SharedFriendsSystem
{
    public bool TryGetFactionDataContainer([NotNullWhen(true)] out Entity<FactionDataContainerComponent>? ent)
    {
        ent = null;
        var query = EntityQueryEnumerator<FactionDataContainerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            ent = (uid, comp);
            return true;
        }

        return false;
    }

    public bool TryGetFactionMemberData(int id, [NotNullWhen(true)] out FactionMemberData? data)
    {
        data = null;
        if (!TryGetFactionDataContainer(out var container))
            return false;

        foreach (var item in container.Value.Comp.CachedMembers)
        {
            if (item.Value.TryGetValue(id, out data))
                return true;
        }

        return false;
    }

    public bool TryGetFactionGroupObjective(ProtoId<MedievalFactionPrototype> proto, FactionMemberGroup group, [NotNullWhen(true)] out string? val)
    {
        val = null;
        if (!TryGetFactionDataContainer(out var container))
            return false;

        return container.Value.Comp.Objectives.TryGetValue(proto, out var objectives) && objectives.TryGetValue(group, out val);
    }

    public bool GetFactionMemberById(int id, [NotNullWhen(true)] out EntityUid? entity)
    {
        entity = null;

        var query = EntityQueryEnumerator<FriendsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.MemberID == id)
            {
                entity = uid;
                return true;
            }
        }
        return false;
    }
}
