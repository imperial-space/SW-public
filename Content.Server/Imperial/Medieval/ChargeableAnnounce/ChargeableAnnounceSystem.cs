using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.ChargeableAnnounce;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Speech;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.ChargeableAnnounce;

public sealed class ChargeableAnnounceSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChargeableAnnounceComponent, UseInHandEvent>(OnUseInHand);
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
}
