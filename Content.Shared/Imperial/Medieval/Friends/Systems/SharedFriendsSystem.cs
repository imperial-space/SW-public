using Content.Shared.Examine;
using Content.Shared.Friends.Components;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;

namespace Content.Shared.Friends;
public abstract class SharedFriendsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FriendsComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, FriendsComponent comp, ExaminedEvent args)
    {
        if (!TryComp<FriendsComponent>(args.Examiner, out var me) || uid == args.Examiner)
            return;
        if (!Identity.Entity(uid, EntityManager).Equals(Name(uid)))
            return;

        string job = comp.MemberData.Job;

        var myFaction = _proto.Index(me.Faction);
        var otherFaction = _proto.Index(comp.Faction);

        if (myFaction == otherFaction && myFaction.ShowKnown)
            args.PushMarkup("[color=green]Из моей фракции, узнаю [/color] " + job);
        else if (myFaction.KnownFactions.TryGetValue(comp.Faction, out var str))
            args.PushMarkup(str);
    }
}
