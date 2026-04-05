using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Server.Imperial.Medieval.Factions.Components;
using Content.Shared.Paper;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Content.Server.Administration.Managers;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class MedievalFactionsSystem
{
    [Dependency] private readonly IChatManager _chatMan = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly IBanManager _ban = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    private void InitializeRelations()
    {
        SubscribeLocalEvent<MedievalFactionMemberComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<FactionDataContainerComponent, MapInitEvent>(OnFactionDataContainerInit);
        SubscribeLocalEvent<MedievalRelationRequestPaperComponent, GetVerbsEvent<AlternativeVerb>>(OnGetRequestVerbs);
        SubscribeNetworkEvent<OfferFactionRelationsEvent>(OnOfferRelations);
        SubscribeNetworkEvent<AcceptFactionRelationsEvent>(OnAcceptRelations);
        SubscribeNetworkEvent<SetFactionRelationsByRequestEvent>(OnSetRelationsByRequest);
        SubscribeNetworkEvent<CreateFactionRelationsRequestEvent>(OnCreateRequest);
        SubscribeNetworkEvent<DispatchWarEvent>(OnDispatchWar);
    }

    private void OnGetAltVerbs(EntityUid uid, MedievalFactionMemberComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (comp.MenuAccess != FactionMenuAccess.Full)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var friends) || friends.MenuAccess != FactionMenuAccess.Full)
            return;

        if (friends.Faction == comp.Faction)
            return;

        if (Proto.Index(friends.Faction).BlockedRelations.Contains(comp.Faction) ||
            Proto.Index(comp.Faction).BlockedRelations.Contains(friends.Faction))
            return;

        AlternativeVerb verb = new()
        {
            Text = Loc.GetString("medieval-hm-relations-change"),
            Act = () =>
            {
                var ev = new OpenOfferFactionRelationsEvent(GetNetEntity(uid), friends.Faction, comp.Faction);
                RaiseNetworkEvent(ev, args.User);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnGetRequestVerbs(EntityUid uid, MedievalRelationRequestPaperComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var friends) || friends.MenuAccess != FactionMenuAccess.Full)
            return;

        if (HasComp<MedievalFactionRelationsRequestComponent>(uid))
            return;

        AlternativeVerb verb = new()
        {
            Text = Loc.GetString("medieval-hm-relations-changereq"),
            Act = () =>
            {
                var ev = new OpenFactionRelationsRequestEvent(GetNetEntity(uid), friends.Faction);
                RaiseNetworkEvent(ev, args.User);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnOfferRelations(OfferFactionRelationsEvent ev, EntitySessionEventArgs args)
    {
        var senderSession = args.SenderSession;
        var senderUid = senderSession.AttachedEntity;
        if (senderUid == null)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }
        if (!TryComp<MedievalFactionMemberComponent>(senderUid, out var friends) || friends.MenuAccess != FactionMenuAccess.Full || friends.Faction != ev.UserFaction)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }

        var targetUid = GetEntity(ev.Target);
        StorePendingRelationsOffer(
            targetUid,
            ev.UserFaction,
            ev.TargetFaction,
            ev.Relation,
            senderUid.Value);

        var openEv = new OpenAcceptFactionRelationsEvent(ev.UserFaction, ev.TargetFaction, ev.Relation);
        RaiseNetworkEvent(openEv, targetUid);
    }

    private void OnAcceptRelations(AcceptFactionRelationsEvent ev, EntitySessionEventArgs args)
    {
        var senderSession = args.SenderSession;
        var senderUid = senderSession.AttachedEntity;
        if (senderUid == null)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }
        if (!TryComp<MedievalFactionMemberComponent>(senderUid, out var friends) || friends.MenuAccess != FactionMenuAccess.Full || friends.Faction != ev.TargetFaction)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }

        EntityUid? offeredBy = null;
        TryTakePendingRelationsOffer(senderUid.Value, ev.UserFaction, ev.TargetFaction, ev.Relation, out offeredBy);

        SetRelations(ev.UserFaction, ev.TargetFaction, ev.Relation);
        LogRelationsChanged(offeredBy, senderUid.Value, ev.UserFaction, ev.TargetFaction, ev.Relation);
    }

    private void OnSetRelationsByRequest(SetFactionRelationsByRequestEvent ev, EntitySessionEventArgs args)
    {
        var targetUid = GetEntity(ev.Target);
        if (!TryComp<MedievalFactionRelationsRequestComponent>(targetUid, out var request))
            return;

        var senderSession = args.SenderSession;
        var senderUid = senderSession.AttachedEntity;
        if (senderUid == null)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }
        if (!TryComp<MedievalFactionMemberComponent>(senderUid, out var friends) || friends.MenuAccess != FactionMenuAccess.Full || friends.Faction != request.To)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }
        if (ev.Decline)
        {
            RemComp<MedievalFactionRelationsRequestComponent>(targetUid);
            RemComp<MedievalFactionRelationsRequestInitiatorComponent>(targetUid);
            return;
        }

        SetRelations(request.From, request.To, request.Relation);

        EntityUid? requestedBy = null;
        if (TryComp<MedievalFactionRelationsRequestInitiatorComponent>(targetUid, out var initiatorComp))
            requestedBy = initiatorComp.RequestedBy;

        LogRelationsChanged(requestedBy, senderUid.Value, request.From, request.To, request.Relation);
        RemComp<MedievalFactionRelationsRequestComponent>(targetUid);
        RemComp<MedievalFactionRelationsRequestInitiatorComponent>(targetUid);
    }

    private void OnCreateRequest(CreateFactionRelationsRequestEvent ev, EntitySessionEventArgs args)
    {
        var senderSession = args.SenderSession;
        var senderUid = senderSession.AttachedEntity;
        if (senderUid == null)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }
        if (!TryComp<MedievalFactionMemberComponent>(senderUid, out var friends) || friends.MenuAccess != FactionMenuAccess.Full || friends.Faction != ev.UserFaction)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }
        var target = GetEntity(ev.Target);
        var faction = Proto.Index(ev.UserFaction);

        var coords = Transform(target).Coordinates;
        if (_container.TryGetContainingContainer(target, out var container))
            coords = Transform(container.Owner).Coordinates;

        var env = Spawn(faction.EnvelopeProto, coords);

        var comp = EnsureComp<MedievalFactionRelationsRequestComponent>(env);
        comp.From = ev.UserFaction;
        comp.To = ev.TargetFaction;
        comp.Relation = ev.Relation;
        Dirty(env, comp);

        var initiatorComp = EnsureComp<MedievalFactionRelationsRequestInitiatorComponent>(env);
        initiatorComp.RequestedBy = senderUid.Value;

        _paper.SetContent(env, Comp<PaperComponent>(target).Content);

        Comp<PaperComponent>(target).EditingDisabled = true;
        QueueDel(target);

        if (container != null)
            _container.InsertOrDrop(env, container);
    }
    private void BanPerson(ICommonSession session, string mes)
    {
        _ban.CreateServerBan(session.UserId, session.Name, null, null, null, 0, Shared.Database.NoteSeverity.High, mes);
    }
    private void OnDispatchWar(DispatchWarEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetFactionDataContainer(out var cont))
            return;

        var senderSession = args.SenderSession;
        var senderUid = senderSession.AttachedEntity;
        if (senderUid == null)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }
        if (!TryComp<MedievalFactionMemberComponent>(senderUid, out var friends) || friends.MenuAccess != FactionMenuAccess.Full || friends.Faction != ev.UserFaction)
        {
            BanPerson(senderSession, Loc.GetString("medieval-relations-error"));
            return;
        }

        _adminLogger.Add(LogType.MedievalFactionRelations, LogImpact.Medium,
            $"Лидер {ToPrettyString(senderUid.Value):leader} фракции {Proto.Index(ev.UserFaction).Name} обьявил войну этой фракции: {Proto.Index(ev.TargetFaction).Name}");

        ref var relations = ref cont.Value.Comp.Relations;
        relations[ev.UserFaction][ev.TargetFaction] = "War";
        relations[ev.TargetFaction][ev.UserFaction] = "War";
        Dirty(cont.Value);

        var userFaction = Proto.Index(ev.UserFaction);
        var targetFaction = Proto.Index(ev.TargetFaction);

        var userMembers = cont.Value.Comp.CachedMembers.GetOrNew(ev.UserFaction);
        var targetMembers = cont.Value.Comp.CachedMembers.GetOrNew(ev.TargetFaction);

        foreach (var item in userMembers.Union(targetMembers))
        {
            if (!GetFactionMemberById(item.Key, out var target) || !_sharedPlayerManager.TryGetSessionByEntity(target.Value, out var session))
                continue;
            var name1 = item.Value.Faction == ev.UserFaction ? targetFaction.Name : userFaction.Name;
            var name2 = Proto.Index<FactionRelationsPrototype>("War").Name;
            var announcement = Loc.GetString("medieval-hm-relations-changeanc", ("name1", $"{name1}"), ("name2", $"{name2}"));
            _chatMan.ChatMessageToOne(Shared.Chat.ChatChannel.Radio, announcement, announcement, EntityUid.Invalid, false, session.Channel, Proto.Index<FactionRelationsPrototype>("War").Color);
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/faction_group_assigned.ogg"), session);
        }
    }

    private void SetRelations(ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction, ProtoId<FactionRelationsPrototype> relation)
    {
        if (!TryGetFactionDataContainer(out var cont))
            return;

        ref var relations = ref cont.Value.Comp.Relations;
        relations[userFaction][targetFaction] = relation;
        relations[targetFaction][userFaction] = relation;
        Dirty(cont.Value);

        var userFactionProto = Proto.Index(userFaction);
        var targetFactionProto = Proto.Index(targetFaction);
        var userMembers = cont.Value.Comp.CachedMembers.GetOrNew(userFaction);
        var targetMembers = cont.Value.Comp.CachedMembers.GetOrNew(targetFaction);

        foreach (var item in userMembers.Union(targetMembers))
        {
            if (!GetFactionMemberById(item.Key, out var target) || !_sharedPlayerManager.TryGetSessionByEntity(target.Value, out var session))
                continue;
            var name1 = item.Value.Faction == userFaction ? targetFactionProto.Name : userFactionProto.Name;
            var name2 = Proto.Index(relation).Name;
            var announcement = Loc.GetString("medieval-hm-relations-changeanc", ("name1", $"{name1}"), ("name2", $"{name2}"));
            _chatMan.ChatMessageToOne(Shared.Chat.ChatChannel.Radio, announcement, announcement, EntityUid.Invalid, false, session.Channel, Proto.Index(relation).Color);
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/faction_group_assigned.ogg"), session);
        }
    }

    private void StorePendingRelationsOffer(
        EntityUid targetUid,
        ProtoId<MedievalFactionPrototype> userFaction,
        ProtoId<MedievalFactionPrototype> targetFaction,
        ProtoId<FactionRelationsPrototype> relation,
        EntityUid offeredBy)
    {
        var pendingComp = EnsureComp<MedievalFactionRelationsPendingOffersComponent>(targetUid);
        pendingComp.Offers.RemoveAll(offer => offer.UserFaction == userFaction && offer.TargetFaction == targetFaction);
        pendingComp.Offers.Add(new MedievalFactionRelationsPendingOfferData
        {
            UserFaction = userFaction,
            TargetFaction = targetFaction,
            Relation = relation,
            OfferedBy = offeredBy
        });
    }

    private bool TryTakePendingRelationsOffer(
        EntityUid targetUid,
        ProtoId<MedievalFactionPrototype> userFaction,
        ProtoId<MedievalFactionPrototype> targetFaction,
        ProtoId<FactionRelationsPrototype> relation,
        out EntityUid? offeredBy)
    {
        offeredBy = null;
        if (!TryComp<MedievalFactionRelationsPendingOffersComponent>(targetUid, out var pendingComp))
            return false;

        for (var i = 0; i < pendingComp.Offers.Count; i++)
        {
            var offer = pendingComp.Offers[i];
            if (offer.UserFaction != userFaction || offer.TargetFaction != targetFaction || offer.Relation != relation)
                continue;

            offeredBy = offer.OfferedBy;
            pendingComp.Offers.RemoveAt(i);
            if (pendingComp.Offers.Count == 0)
                RemComp<MedievalFactionRelationsPendingOffersComponent>(targetUid);

            return true;
        }

        return false;
    }

    private void LogRelationsChanged(
        EntityUid? offeredBy,
        EntityUid acceptedBy,
        ProtoId<MedievalFactionPrototype> userFaction,
        ProtoId<MedievalFactionPrototype> targetFaction,
        ProtoId<FactionRelationsPrototype> relation)
    {
        if (offeredBy != null)
        {
            _adminLogger.Add(LogType.MedievalFactionRelations, LogImpact.Medium,
                $"лидеры фракций {ToPrettyString(offeredBy.Value):leader} и {ToPrettyString(acceptedBy):leader} изменили отношения между фракциями {Proto.Index(userFaction).Name} и {Proto.Index(targetFaction).Name} на {relation.Id}");
            return;
        }

        _adminLogger.Add(LogType.MedievalFactionRelations, LogImpact.Medium,
            $"лидеры фракций неизвестно и {ToPrettyString(acceptedBy):leader} изменили отношения между фракциями {Proto.Index(userFaction).Name} и {Proto.Index(targetFaction).Name} на {relation.Id}");
    }

    private void OnFactionDataContainerInit(EntityUid uid, FactionDataContainerComponent comp, MapInitEvent args)
    {
        var factions = Proto.EnumeratePrototypes<MedievalFactionPrototype>();
        foreach (var item in factions)
        {
            foreach (var item2 in factions)
            {
                if (item == item2)
                    continue;

                comp.Relations.TryAdd(item.ID, new());
                comp.Relations[item.ID].Add(item2.ID, item.DefaultRelations.GetValueOrDefault(item2.ID, "Neutral"));
            }
        }

        Dirty(uid, comp);
    }
}
