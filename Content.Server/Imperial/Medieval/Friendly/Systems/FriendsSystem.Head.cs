using Content.Server.MedievalPasport.Components;
using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Friends;

public sealed partial class FriendsSystem
{
    private void InitializeHead()
    {
        SubscribeLocalEvent<FriendsComponent, MapInitEvent>(OnFriendsInit);

        SubscribeLocalEvent<FactionHeadComponent, MapInitEvent>(OnHeadInit);

        SubscribeNetworkEvent<SetFactionMemberObjectiveMessage>(OnSetObjective);
        SubscribeNetworkEvent<SetFactionMemberGroupMessage>(OnSetGroup);
        SubscribeLocalEvent<RemoveFactionMemberMessage>(OnMemberRemoved);
    }

    private void OnFriendsInit(EntityUid uid, FriendsComponent comp, MapInitEvent args)
    {
        comp.MemberData = new()
        {
            Name = Name(uid),
            Job = CompOrNull<MedievalPasportPersonComponent>(uid)?.PersonJob ?? "Нет должности"
        };

        Dirty(uid, comp);

        RefreshFactionHeads(comp.Faction);
    }

    private void OnHeadInit(EntityUid uid, FactionHeadComponent comp, MapInitEvent args)
    {
        if (!TryComp<FriendsComponent>(uid, out var friends))
            return;

        RefreshFactionHeads(friends.Faction);
        _action.AddAction(uid, ref comp.FactionMenuActionEntity, comp.FactionMenuAction);
    }

    private void OnSetObjective(SetFactionMemberObjectiveMessage args)
    {
        var uid = GetEntity(args.Ent);
        if (!uid.IsValid())
            return;
        if (!TryComp<FriendsComponent>(uid, out var comp))
            return;

        comp.MemberData.Objective = args.Objective;
        RefreshFactionHeads(comp.Faction);
    }

    private void OnSetGroup(SetFactionMemberGroupMessage args)
    {
        var uid = GetEntity(args.Ent);
        if (!uid.IsValid())
            return;
        if (!TryComp<FriendsComponent>(uid, out var comp))
            return;

        comp.MemberData.Group = args.Group;
        RefreshFactionHeads(comp.Faction);
    }

    private void OnMemberRemoved(RemoveFactionMemberMessage args)
    {
        var uid = GetEntity(args.Ent);
        if (!uid.IsValid())
            return;
        if (!TryComp<FriendsComponent>(uid, out var comp))
            return;
        comp.Faction = "Voluntary";
        comp.MemberData.Job = "Нет должности";
        comp.MemberData.Objective = "";
        comp.MemberData.Group = "";
    }

    public void RefreshFactionHeads(ProtoId<MedievalFactionPrototype> proto)
    {
        Dictionary<NetEntity, FactionMemberData> dict = new();

        var query = EntityQueryEnumerator<FriendsComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            dict.Add(GetNetEntity(uid), member.MemberData);
        }

        var headQuery = EntityQueryEnumerator<FactionHeadComponent>();
        while (headQuery.MoveNext(out var uid, out var head))
        {
            head.CachedMembers = dict;
            Dirty(uid, head);
        }
    }
}
