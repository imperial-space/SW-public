using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Trade;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server.Trade;

public sealed partial class TradeTerminalSystem
{
    private void OnActivate(EntityUid uid, TradeTerminalComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!TryOpenTradeUi(uid, comp, args.User, actor.PlayerSession))
            return;

        args.Handled = true;
    }

    private void OnGetVerbs(EntityUid uid, TradeTerminalComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<ActorComponent>(args.User, out _))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("trade-terminal-verb-open"),
            Priority = 1,
            Act = () =>
            {
                if (!TryComp<ActorComponent>(args.User, out var actor))
                    return;

                TryOpenTradeUi(uid, comp, args.User, actor.PlayerSession);
            },
        });
    }

    private bool TryOpenTradeUi(EntityUid uid, TradeTerminalComponent comp, EntityUid user, ICommonSession session)
    {
        if (!TryClaimOwner(uid, comp, user))
            return false;

        _ui.OpenUi(uid, TradeUiKey.Key, session);
        UpdateUi(uid, comp);
        return true;
    }

    private bool TryClaimOwner(EntityUid uid, TradeTerminalComponent comp, EntityUid user)
    {
        if (comp.Owner != null && comp.Owner != user)
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-busy"), uid, user);
            return false;
        }

        if (TryGetOwnedTerminal(user, out var activeTerminalUid, out _) &&
            activeTerminalUid != uid)
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-already-using"), uid, user);
            return false;
        }

        if (comp.Owner == null)
        {
            if (comp.State == TradeSessionState.Completed)
            {
                _popup.PopupEntity(Loc.GetString("trade-terminal-busy"), uid, user);
                return false;
            }

            comp.Owner = user;
        }

        _activeUsers[user] = uid;
        return true;
    }

    private void ReleaseOwner(TradeTerminalComponent comp, EntityUid user)
    {
        if (comp.Owner != user)
            return;

        _activeUsers.Remove(user);
        comp.Owner = null;
    }

    private void OnUIClosed(EntityUid uid, TradeTerminalComponent comp, BoundUIClosedEvent args)
    {
        if (!args.UiKey.Equals(TradeUiKey.Key) || comp.Owner != args.Actor)
            return;

        if (comp.State == TradeSessionState.Idle)
        {
            ReleaseOwner(comp, args.Actor);
            return;
        }

        if (comp.State == TradeSessionState.Completed)
            return;

        HangUp(uid, comp);
    }

    private void OnPlayerDamaged(EntityUid uid, ActorComponent component, DamageChangedEvent args)
    {
        if (args.DamageIncreased &&
            _activeUsers.TryGetValue(uid, out var terminalUid) &&
            TryComp<TradeTerminalComponent>(terminalUid, out var comp))
        {
            ForceCloseAndHangUp(terminalUid, comp, uid);
        }
    }

    private void OnMobStateChanged(EntityUid uid, ActorComponent component, MobStateChangedEvent args)
    {
        if ((args.NewMobState == MobState.Critical || args.NewMobState == MobState.Dead) &&
            _activeUsers.TryGetValue(uid, out var terminalUid) &&
            TryComp<TradeTerminalComponent>(terminalUid, out var comp))
        {
            ForceCloseAndHangUp(terminalUid, comp, uid);
        }
    }

    private void ForceCloseAndHangUp(EntityUid uid, TradeTerminalComponent comp, EntityUid userUid)
    {
        if (comp.State == TradeSessionState.Idle)
        {
            ReleaseOwner(comp, userUid);
        }
        else if (comp.State != TradeSessionState.Completed)
        {
            HangUp(uid, comp);
        }

        _ui.CloseUi(uid, TradeUiKey.Key);
    }
}
