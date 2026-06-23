using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.ChargeableAnnounce;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Speech;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.ChargeableAnnounce;

public sealed class ChargeableAnnounceSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChargeableAnnounceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ChargeableAnnounceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChargeableAnnounceComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<ChargeableAnnounceComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(EntityUid uid, ChargeableAnnounceComponent comp, MapInitEvent args)
    {
        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        TryBindFromContainerHierarchy(uid, comp, receiver.Faction);
    }

    private void OnInsertedIntoContainer(EntityUid uid, ChargeableAnnounceComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (comp.OwnerUid.HasValue)
            return;

        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        TryBindFromContainerHierarchy(uid, comp, receiver.Faction, args.Container.Owner);
    }

    private void OnExamined(EntityUid uid, ChargeableAnnounceComponent comp, ExaminedEvent args)
    {
        if (!comp.OwnerUid.HasValue)
            args.PushMarkup("[color=gray]Кристалл бесхозный.[/color]");
    }

    private void OnUseInHand(EntityUid uid, ChargeableAnnounceComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        if (!comp.IsCharged)
        {
            _popup.PopupEntity("Кристалл связи разряжен.", uid, args.User);
            return;
        }

        if (comp.OwnerUid.HasValue && comp.OwnerUid.Value != args.User)
        {
            _popup.PopupEntity("Это не ваш кристалл связи.", uid, args.User);
            return;
        }

        if (!_sharedPlayerManager.TryGetSessionByEntity(args.User, out var session))
            return;

        args.Handled = true;

        _quickDialog.OpenDialog(session, "Весть", "Сообщение", (string message) =>
        {
            if (string.IsNullOrWhiteSpace(message) || Deleted(uid) || !TryComp<ChargeableAnnounceComponent>(uid, out var announce))
                return;

            var query = EntityQueryEnumerator<CloackRecieverComponent>();
            while (query.MoveNext(out var cloackOwner, out var cloack))
            {
                if (cloack.Faction != receiver.Faction)
                    continue;

                EnsureComp<SpeechComponent>(cloackOwner);
                _chat.TrySendInGameICMessage(cloackOwner, message, InGameICChatType.Whisper, false);
            }

            announce.IsCharged = false;
            Dirty(uid, announce);
        });
    }

    private void TryBindFromContainerHierarchy(EntityUid crystalUid, ChargeableAnnounceComponent comp, string crystalFaction, EntityUid? startEntity = null)
    {
        EntityUid current;

        if (startEntity.HasValue)
        {
            current = startEntity.Value;
        }
        else
        {
            if (!_container.TryGetContainingContainer(crystalUid, out var rootContainer))
                return;
            current = rootContainer.Owner;
        }

        while (true)
        {
            if (TryComp<MedievalFactionMemberComponent>(current, out var factionMember))
            {
                if (factionMember.Faction == crystalFaction)
                {
                    comp.OwnerUid = current;
                    Dirty(crystalUid, comp);
                }
                return;
            }

            if (!_container.TryGetContainingContainer(current, out var parentContainer))
                return;

            current = parentContainer.Owner;
        }
    }
}
