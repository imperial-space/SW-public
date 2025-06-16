using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Content.Shared.Imperial.Medieval.CombatStance;
using Robust.Shared.Physics.Events;
using Content.Shared.Friends.Components;
using Content.Server.Friends;
using Content.Shared.Mobs;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Humanoid;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Server.GameStates;
using Robust.Shared.Player;
using Content.Server.Imperial.PVS;
using Content.Shared.Friends;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Events;

namespace Content.Server.Imperial.Medieval.CombatStance;

public sealed class CombatStancePointTestSystem : EntitySystem
{
    [Dependency] private readonly FriendsSystem _friends = default!;
    [Dependency] private readonly EntityLookupSystem _look = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly AlwaysPvsSystem _pvs = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<CombatStancePointComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<CombatStancePointComponent, EndCollideEvent>(EndCollide);
        SubscribeLocalEvent<FriendsComponent, CombatStancePointEvent>(Prompt);
        _net.RegisterNetMessage<CombatStancePointPlaceMessage>(PlacePoint);
        _net.RegisterNetMessage<CombatStancePointRemoveMessage>(RemovePoints);
        SubscribeLocalEvent<CombatStanceComponent, DamageModifyEvent>(ModifyDamage);
        SubscribeLocalEvent<CombatStanceComponent, StaminaModifyEvent>(StaminaModifyDamage);
    }
    private void StaminaModifyDamage(EntityUid uid, CombatStanceComponent component, StaminaModifyEvent args)
    {
        if (!component.HasDefence)
            return;
        if (args.Damage > 0)
            args.Damage = args.Damage * 0.5f;
    }
    private void ModifyDamage(EntityUid uid, CombatStanceComponent component, DamageModifyEvent args)
    {
        if (!component.HasDefence)
            return;
        var newdict = new Dictionary<string, FixedPoint2>();
        foreach (var (key, value) in args.Damage.DamageDict)
        {
            Console.WriteLine(value.Float());
            if (value > 0)
                newdict[key] = FixedPoint2.New(value.Float() * 0.5f);
            else
                newdict[key] = value;
            Console.WriteLine(newdict[key].Float());
        }
        args.Damage.DamageDict = newdict;
    }
    public void MemberRemoved(EntityUid uid, string faction, FactionMemberGroup group)
    {
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        if (!_players.TryGetSessionByEntity(uid, out var session))
            return;
        if (!TryComp<FriendsComponent>(uid, out var friends))
            return;
        if (friends.MenuAccess == FactionMenuAccess.Full)
            return;
        foreach (var point in factions.Value.Comp.Points.GetOrNew(faction).GetOrNew(group))
        {
            _pvs.RemoveForceSend(point, session);
        }
    }
    public void GroupChanged(EntityUid uid, string faction, FactionMemberGroup group, FactionMemberGroup oldgroup)
    {
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        if (!_players.TryGetSessionByEntity(uid, out var session))
            return;
        if (oldgroup != FactionMemberGroup.None)
        {
            foreach (var point in factions.Value.Comp.Points.GetOrNew(faction).GetOrNew(oldgroup))
            {
                _pvs.RemoveForceSend(point, session);
            }
        }
        if (group != FactionMemberGroup.None)
        {
            foreach (var point in factions.Value.Comp.Points.GetOrNew(faction).GetOrNew(group))
            {
                _pvs.AddForceSend(point, session);
            }
        }
    }
    private void RemovePoints(CombatStancePointRemoveMessage msg)
    {
        var player = _players.GetSessionByChannel(msg.MsgChannel);
        var uid = player.AttachedEntity;
        if (uid == null)
            return;
        if (!TryComp<FriendsComponent>(uid, out var friends))
            return;
        if (friends.MenuAccess != FactionMenuAccess.Full)
            return;
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        var points = factions.Value.Comp.Points.GetOrNew(friends.Faction).GetOrNew(msg.Group);
        foreach (var point in points)
        {
            if (Deleted(point))
                continue;
            QueueDel(point);
        }
        points.Clear();
    }
    private void PlacePoint(CombatStancePointPlaceMessage msg)
    {
        var player = _players.GetSessionByChannel(msg.MsgChannel);
        var uid = player.AttachedEntity;
        if (uid == null)
            return;
        if (!TryComp<FriendsComponent>(uid, out var friends))
            return;
        if (friends.MenuAccess != FactionMenuAccess.Full)
            return;
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        if (!factions.Value.Comp.CachedMembers.TryGetValue(friends.Faction, out var members))
            return;
        var pvslist = new List<ICommonSession>() { player };
        foreach (var (id, member) in members)
        {
            if (id == friends.MemberID)
                continue;
            if (member.Group != msg.Group)
                continue;
            if (!_friends.GetFactionMemberById(id, out var memberuid))
                continue;
            if (!_players.TryGetSessionByEntity(memberuid.Value, out var membersession))
                continue;
            pvslist.Add(membersession);
        }
        var points = factions.Value.Comp.Points.GetOrNew(friends.Faction).GetOrNew(msg.Group);
        var toremove = new List<EntityUid>();
        var count = 0;
        foreach (var point in points)
        {
            if (Deleted(point))
            {
                toremove.Add(point);
                continue;
            }
            count++;
        }
        foreach (var point in toremove)
        {
            points.Remove(point);
        }
        if (count >= pvslist.Count)
        {
            return;
        }
        var ent = Spawn("StancePoint", EntityManager.GetCoordinates(msg.Coords));
        foreach (var session in pvslist)
        {
            _pvs.AddForceSend(ent, session);
        }
        var newpoint = EnsureComp<CombatStancePointComponent>(ent);
        newpoint.Group = msg.Group;
        newpoint.Faction = friends.Faction;
        points.Add(ent);
        Init(ent, newpoint);
    }
    private void Prompt(EntityUid uid, FriendsComponent component, CombatStancePointEvent args)
    {
        if (!_players.TryGetSessionByEntity(uid, out var session))
            return;
        RaiseNetworkEvent(new CombatStanceMenuEvent(), session);
    }
    private bool TryMatch(EntityUid uid, CombatStancePointComponent ourcomponent, int x, int y, [NotNullWhen(true)] out EntityUid? result)
    {
        result = null;
        var coords = Transform(uid).MapPosition.Offset(x, y);
        foreach (var ent in _look.GetEntitiesInRange(coords, 0.7f, LookupFlags.All))
        {
            if (ent == uid)
                continue;
            if (!TryComp<CombatStancePointComponent>(ent, out var component))
                continue;

            if (component.Faction != ourcomponent.Faction || component.Group != ourcomponent.Group)
                continue;

            result = ent;
            return true;
        }

        return false;
    }
    private void Init(EntityUid uid, CombatStancePointComponent component)
    {
        if (TryMatch(uid, component, 1, 0, out var rightresult))
        {
            component.PointDirection.right = true;
            component.PointDirectionData.right = rightresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(rightresult.Value);
            othercomp.PointDirection.left = true;
            othercomp.PointDirectionData.left = uid;
        }
        if (TryMatch(uid, component, -1, 0, out var leftresult))
        {
            component.PointDirection.left = true;
            component.PointDirectionData.left = leftresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(leftresult.Value);
            othercomp.PointDirection.right = true;
            othercomp.PointDirectionData.right = uid;
        }
        if (TryMatch(uid, component, 0, 1, out var upresult))
        {
            component.PointDirection.up = true;
            component.PointDirectionData.up = upresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(upresult.Value);
            othercomp.PointDirection.bottom = true;
            othercomp.PointDirectionData.bottom = uid;
        }
        if (TryMatch(uid, component, 0, -1, out var bottomresult))
        {
            component.PointDirection.bottom = true;
            component.PointDirectionData.bottom = bottomresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(bottomresult.Value);
            othercomp.PointDirection.up = true;
            othercomp.PointDirectionData.up = uid;
        }
    }

    private void OnCollide(EntityUid uid, CombatStancePointComponent component, StartCollideEvent args)
    {
        if (!TryComp<FriendsComponent>(args.OtherEntity, out var friends))
            return;
        if (!_friends.TryGetFactionMemberData(friends.MemberID, out var data))
            return;
        if (data.Group != component.Group || data.Faction != component.Faction)
            return;
        if (!HasComp<CombatStanceComponent>(args.OtherEntity))
            return;
        if (component.ValidMembers.Contains(args.OtherEntity))
            return;
        component.ValidMembers.Add(args.OtherEntity);
        ListChanged(uid, component);
    }
    private void EndCollide(EntityUid uid, CombatStancePointComponent component, EndCollideEvent args)
    {
        if (!component.ValidMembers.Contains(args.OtherEntity))
            return;
        component.ValidMembers.Remove(args.OtherEntity);
        if (TryComp<CombatStanceComponent>(args.OtherEntity, out var comp))
            comp.HasDefence = false;
        ListChanged(uid, component);
    }
    public int Recursive(EntityUid uid, CombatStancePointComponent component, int current, ref List<EntityUid> blacklist, ref List<EntityUid> participants)
    {
        blacklist.Add(uid);
        if (component.HasValidMember)
        {
            foreach (var part in component.ValidMembers)
            {
                if (participants.Contains(part))
                    continue;
                current += 1;
                participants.Add(part);
                break;
            }
        }
        if (component.PointDirection.up && component.PointDirectionData.up.HasValue && !Deleted(component.PointDirectionData.up) && !blacklist.Contains(component.PointDirectionData.up.Value))
        {
            current = Recursive(component.PointDirectionData.up.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.up.Value), current, ref blacklist, ref participants);
        }
        if (component.PointDirection.bottom && component.PointDirectionData.bottom.HasValue && !Deleted(component.PointDirectionData.bottom) && !blacklist.Contains(component.PointDirectionData.bottom.Value))
        {
            current = Recursive(component.PointDirectionData.bottom.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.bottom.Value), current, ref blacklist, ref participants);
        }
        if (component.PointDirection.right && component.PointDirectionData.right.HasValue && !Deleted(component.PointDirectionData.right) && !blacklist.Contains(component.PointDirectionData.right.Value))
        {
            current = Recursive(component.PointDirectionData.right.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.right.Value), current, ref blacklist, ref participants);
        }
        if (component.PointDirection.left && component.PointDirectionData.left.HasValue && !Deleted(component.PointDirectionData.left) && !blacklist.Contains(component.PointDirectionData.left.Value))
        {
            current = Recursive(component.PointDirectionData.left.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.left.Value), current, ref blacklist, ref participants);
        }
        return current;
    }
    private void ListChanged(EntityUid uid, CombatStancePointComponent component)
    {
        var recursed = new List<EntityUid>();
        var participants = new List<EntityUid>();
        var amountofvalid = Recursive(uid, component, 0, ref recursed, ref participants);
        foreach (var recurs in recursed)
        {
            var comp = EnsureComp<CombatStancePointComponent>(recurs);
            comp.Participants = amountofvalid;
            UpdateState(recurs, comp);
        }
        foreach (var member in participants)
        {
            var stance = EnsureComp<CombatStanceComponent>(member);
            if (amountofvalid >= 4)
                stance.HasDefence = true;
            else
                stance.HasDefence = false;
        }
    }
    private void UpdateState(EntityUid uid, CombatStancePointComponent component)
    {
        if (component.Participants <= 0)
        {
            _appearance.SetData(uid, CombatStanceAppearance.Key, 0);
            return;
        }
        if (component.Participants >= 4)
        {
            _appearance.SetData(uid, CombatStanceAppearance.Key, 2);
            return;
        }
        if (component.Participants < 4)
        {
            _appearance.SetData(uid, CombatStanceAppearance.Key, 1);
            return;
        }
    }
}
