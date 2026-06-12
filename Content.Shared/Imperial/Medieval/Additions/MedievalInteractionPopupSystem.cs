using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Interaction;

public sealed class MedievalInteractionPopupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalInteractionPopupComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(EntityUid uid, MedievalInteractionPopupComponent component, GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || ev.User == ev.Target)
            return;

        if (!_playerManager.TryGetSessionByEntity(ev.User, out var session))
            return;

        var userId = session.UserId.ToString();

        if (component.AllowedUserIds.Count > 0 && !component.AllowedUserIds.Contains(userId))
            return;

        ev.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryInteract(ev.User, ev.Target, component),
            Text = Loc.GetString("medieval-interact-verb")
        });
    }

    private void TryInteract(EntityUid user, EntityUid target, MedievalInteractionPopupComponent component)
    {
        var curTime = _gameTiming.CurTime;
        if (curTime < component.LastInteractTime + component.InteractDelay)
            return;

        component.LastInteractTime = curTime;

        if (!_playerManager.TryGetSessionByEntity(user, out var session))
            return;

        var userId = session.UserId.ToString();

        if (component.AllowedUserIds.Count > 0 && !component.AllowedUserIds.Contains(userId))
            return;

        if (!string.IsNullOrEmpty(component.PopupMessage))
        {
            var msg = Loc.GetString(component.PopupMessage, ("target", Identity.Entity(target, EntityManager)));
            _popupSystem.PopupEntity(msg, target);
        }

        if (component.Sound != null)
        {
            _audio.PlayPvs(component.Sound, target);
        }

        var ev = new MedievalInteractionEvent(user, target);
        RaiseLocalEvent(target, ref ev);
    }
}
