using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.MedievalPasport.Components;
using Content.Server.Station.Components;
using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Friends;

public sealed partial class FriendsSystem
{
    private int _nextId = 1;

    private void InitializeMenu()
    {
        SubscribeLocalEvent<FriendsComponent, MapInitEvent>(OnFriendsInit);

        SubscribeNetworkEvent<SetFactionMemberObjectiveMessage>(OnSetObjective);
        SubscribeNetworkEvent<SetFactionMemberGroupMessage>(OnSetGroup);
        SubscribeLocalEvent<RemoveFactionMemberMessage>(OnMemberRemoved);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStartedMenu);
    }

    private void OnFriendsInit(EntityUid uid, FriendsComponent comp, MapInitEvent args)
    {
        if (!TryGetFactionDataContainer(out var container))
            return;

        comp.MemberID = _nextId;
        _nextId++;

        var data = new FactionMemberData()
        {
            Name = Name(uid),
            Job = CompOrNull<MedievalPasportPersonComponent>(uid)?.PersonJob ?? "Нет должности"
        };
        container.Value.Comp.CachedMembers.GetOrNew(comp.Faction).Add(comp.MemberID, data);

        _action.AddAction(uid, ref comp.FactionMenuActionEntity, comp.FactionMenuAction);

        Dirty(uid, comp);
        RefreshFactionMenu(comp.Faction);
    }

    private void OnSetObjective(SetFactionMemberObjectiveMessage args)
    {
        if (!TryGetFactionDataContainer(out var ent))
            return;
        var dict = ent.Value.Comp.Objectives.GetOrNew(args.Faction);
        if (dict.TryGetValue(args.Group, out _))
            dict[args.Group] = args.Objective;
        else
            dict.Add(args.Group, args.Objective);

        RefreshFactionMenu(args.Faction);
    }

    private void OnSetGroup(SetFactionMemberGroupMessage args)
    {
        if (!GetFactionMemberById(args.Ent, out var uid))
            return;
        if (!uid.Value.IsValid())
            return;
        if (!TryComp<FriendsComponent>(uid, out var comp))
            return;
        if (!TryGetFactionDataContainer(out var container))
            return;
        if (!TryGetFactionMemberData(args.Ent, out var data))
            return;

        data.Group = args.Group;

        RefreshFactionMenu(comp.Faction);
    }

    private void OnMemberRemoved(RemoveFactionMemberMessage args)
    {
        var uid = GetEntity(args.Ent);
        if (!uid.IsValid())
            return;
        if (!TryComp<FriendsComponent>(uid, out var comp))
            return;
        // comp.Faction = "Voluntary";
        // comp.MemberData.Job = "Нет должности";
        // comp.MemberData.Objective = "";
        //comp.MemberData.Group = "";
    }

    private void OnRoundStartedMenu(RoundStartedEvent args)
    {
        var query = AllEntityQuery<BecomesStationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            EnsureComp<FactionDataContainerComponent>(uid);
            break;
        }
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

    public void RefreshFactionMenu(ProtoId<MedievalFactionPrototype> proto)
    {
        Dictionary<int, FactionMemberData> dict = new();

        if (!TryGetFactionDataContainer(out var container))
            return;

        dict = container.Value.Comp.CachedMembers.GetOrNew(proto);
        var list = dict.ToList();
        list.Sort((x, y) =>
        {
            if (!GetFactionMemberById(x.Key, out var xEnt) || !TryComp<FriendsComponent>(xEnt, out var xFriends))
                return 1;
            if (!GetFactionMemberById(y.Key, out var yEnt) || !TryComp<FriendsComponent>(yEnt, out var yFriends))
                return -1;

            return yFriends.Priority.CompareTo(yFriends.Priority);
        });

        var headQuery = EntityQueryEnumerator<FactionDataContainerComponent>();
        while (headQuery.MoveNext(out var uid, out var data))
        {
            if (data.CachedMembers.TryGetValue(proto, out _))
                data.CachedMembers[proto] = list.ToDictionary();
            else
                data.CachedMembers.Add(proto, list.ToDictionary());
            Dirty(uid, data);
        }
    }
}
