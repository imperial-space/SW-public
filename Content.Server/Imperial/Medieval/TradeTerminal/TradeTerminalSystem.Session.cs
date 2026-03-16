using System.Linq;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Telephone;
using Content.Shared.Trade;
using Robust.Shared.Timing;

namespace Content.Server.Trade;

public sealed partial class TradeTerminalSystem
{
    private void OnCall(EntityUid uid, TradeTerminalComponent comp, TradeCallMessage args)
    {
        if (!IsOwner(comp, args.Actor) || comp.State != TradeSessionState.Idle)
            return;

        var targetUid = GetEntity(args.Target);
        if (targetUid == uid || !TryComp<TradeTerminalComponent>(targetUid, out var target))
            return;

        if (!IsAvailable(target))
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-target-busy"), uid, args.Actor);
            return;
        }

        comp.LinkedTerminal = targetUid;
        target.LinkedTerminal = uid;
        comp.CallTimeoutTime = _timing.CurTime + CallTimeout;
        target.CallTimeoutTime = comp.CallTimeoutTime;

        SetState(uid, comp, TradeSessionState.Calling);
        SetState(targetUid, target, TradeSessionState.Ringing);
        target.NextRingTime = TimeSpan.Zero;

        _jitter.AddJitter(targetUid, 5f, 8f);

        if (target.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-incoming-popup", ("caller", TerminalName(uid))),
                targetUid,
                target.Owner.Value,
                PopupType.Medium);
        }

        UpdateBothUi(uid, comp);
    }

    private void OnAcceptCall(EntityUid uid, TradeTerminalComponent comp, TradeAcceptCallMessage args)
    {
        if (!IsOwner(comp, args.Actor) ||
            comp.State != TradeSessionState.Ringing ||
            !TryGetPartner(comp, out var partnerId, out var partner))
        {
            return;
        }

        comp.CallTimeoutTime = TimeSpan.Zero;
        partner.CallTimeoutTime = TimeSpan.Zero;
        SetState(uid, comp, TradeSessionState.Active);
        SetState(partnerId, partner, TradeSessionState.Active);

        RemComp<JitteringComponent>(uid);

        if (comp.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-connected", ("partner", TerminalName(partnerId))),
                uid,
                comp.Owner.Value);
        }

        if (partner.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-connected", ("partner", TerminalName(uid))),
                partnerId,
                partner.Owner.Value);
        }

        if (TryComp<TelephoneComponent>(uid, out var phoneReceiver) &&
            TryComp<TelephoneComponent>(partnerId, out var phoneCaller))
        {
            var callerEntity = new Entity<TelephoneComponent>(partnerId, phoneCaller);
            var receiverEntity = new Entity<TelephoneComponent>(uid, phoneReceiver);
            var caller = partner.Owner ?? partnerId;

            _telephone.CallTelephone(
                callerEntity,
                receiverEntity,
                caller,
                new TelephoneCallOptions { ForceConnect = true, IgnoreRange = true });
        }

        UpdateBothUi(uid, comp);
    }

    private void OnHangUp(EntityUid uid, TradeTerminalComponent comp, TradeHangUpMessage args)
    {
        if (!IsOwner(comp, args.Actor) || comp.State == TradeSessionState.Completed)
            return;

        HangUp(uid, comp);
    }

    private void HangUp(EntityUid uid, TradeTerminalComponent comp)
    {
        if (comp.State == TradeSessionState.Completed)
            return;

        var cancelledBy = comp.Owner;

        if (TryGetPartner(comp, out var partnerId, out var partner))
        {
            ReturnItemsToWorld(partnerId, partner);

            if (partner.Owner != null)
            {
                _popup.PopupEntity(
                    Loc.GetString("trade-terminal-hung-up", ("partner", TerminalName(uid))),
                    partnerId,
                    partner.Owner.Value);
            }

            ResetTerminal(partnerId, partner);
        }

        ReturnItemsToWorld(uid, comp);
        ResetTerminal(uid, comp);
        RaiseLocalEvent(new TradeCancelledEvent(uid, cancelledBy));
    }

    private void TimeoutCall(EntityUid uid, TradeTerminalComponent comp)
    {
        if (!TryGetPartner(comp, out var partnerId, out var partner))
        {
            ResetTerminal(uid, comp);
            return;
        }

        if (comp.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-call-timed-out"),
                uid,
                comp.Owner.Value);
        }

        if (partner.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-call-timed-out"),
                partnerId,
                partner.Owner.Value);
        }

        ResetTerminal(partnerId, partner);
        ResetTerminal(uid, comp);
        RaiseLocalEvent(new TradeCancelledEvent(uid));
    }

    private void OnConfirm(EntityUid uid, TradeTerminalComponent comp, TradeConfirmMessage args)
    {
        if (!IsOwner(comp, args.Actor) ||
            comp.State != TradeSessionState.Active ||
            !TryGetPartner(comp, out var partnerId, out var partner))
        {
            return;
        }

        comp.HasConfirmed = !comp.HasConfirmed;

        if (comp.HasConfirmed && partner.HasConfirmed)
        {
            var confirmerNet = comp.Owner != null ? GetNetEntity(comp.Owner.Value) : (NetEntity?) null;
            var confirmerName = comp.Owner != null ? Name(comp.Owner.Value) : "???";
            var endTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CountdownDuration);

            SetState(uid, comp, TradeSessionState.Countdown);
            comp.CountdownEndTime = endTime;
            comp.ConfirmedBy = confirmerNet;
            comp.HasConfirmed = false;

            SetState(partnerId, partner, TradeSessionState.Countdown);
            partner.CountdownEndTime = endTime;
            partner.ConfirmedBy = confirmerNet;
            partner.HasConfirmed = false;

            if (comp.Owner != null)
            {
                _popup.PopupEntity(
                    Loc.GetString("trade-terminal-countdown-self", ("seconds", (int) comp.CountdownDuration)),
                    uid,
                    comp.Owner.Value);
            }

            if (partner.Owner != null)
            {
                _popup.PopupEntity(
                    Loc.GetString(
                        "trade-terminal-countdown-partner",
                        ("name", confirmerName),
                        ("seconds", (int) comp.CountdownDuration)),
                    partnerId,
                    partner.Owner.Value,
                    PopupType.Medium);
            }
        }

        UpdateBothUi(uid, comp);
    }

    private void OnCancel(EntityUid uid, TradeTerminalComponent comp, TradeCancelMessage args)
    {
        if (!IsOwner(comp, args.Actor) || comp.State == TradeSessionState.Completed)
            return;

        if (comp.State == TradeSessionState.Countdown)
        {
            CancelCountdown(uid, comp);
            return;
        }

        HangUp(uid, comp);
    }

    private void CancelCountdown(EntityUid uid, TradeTerminalComponent comp)
    {
        if (!TryGetPartner(comp, out var partnerId, out var partner))
            return;

        var cancellerName = comp.Owner != null ? Name(comp.Owner.Value) : "???";

        SetState(uid, comp, TradeSessionState.Active);
        comp.CountdownEndTime = TimeSpan.Zero;
        comp.ConfirmedBy = null;
        comp.HasConfirmed = false;

        SetState(partnerId, partner, TradeSessionState.Active);
        partner.CountdownEndTime = TimeSpan.Zero;
        partner.ConfirmedBy = null;
        partner.HasConfirmed = false;

        if (comp.Owner != null)
            _popup.PopupEntity(Loc.GetString("trade-terminal-countdown-cancelled"), uid, comp.Owner.Value);

        if (partner.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-countdown-cancelled-partner", ("name", cancellerName)),
                partnerId,
                partner.Owner.Value);
        }

        UpdateBothUi(uid, comp);
    }

    private void ExecuteTrade(EntityUid uidA, TradeTerminalComponent compA, EntityUid uidB, TradeTerminalComponent compB)
    {
        if (compA.State != TradeSessionState.Countdown)
            return;

        SetState(uidA, compA, TradeSessionState.Completed);
        compA.CountdownEndTime = TimeSpan.Zero;
        compA.ConfirmedBy = null;
        compA.HasConfirmed = false;

        SetState(uidB, compB, TradeSessionState.Completed);
        compB.CountdownEndTime = TimeSpan.Zero;
        compB.ConfirmedBy = null;
        compB.HasConfirmed = false;

        var completedExpireTime =
            _timing.CurTime + TimeSpan.FromSeconds(MathF.Max(compA.CompletedCleanupDelay, compB.CompletedCleanupDelay));
        compA.CompletedExpireTime = completedExpireTime;
        compB.CompletedExpireTime = completedExpireTime;

        var containerA = GetOfferContainer(uidA, compA);
        var containerB = GetOfferContainer(uidB, compB);

        var itemsA = containerA.ContainedEntities.ToList();
        var itemsB = containerB.ContainedEntities.ToList();

        foreach (var item in itemsA)
        {
            Containers.Remove(item, containerA, force: true);
        }

        foreach (var item in itemsB)
        {
            Containers.Remove(item, containerB, force: true);
        }

        foreach (var item in itemsB)
        {
            Containers.Insert(item, containerA, force: true);
        }

        foreach (var item in itemsA)
        {
            Containers.Insert(item, containerB, force: true);
        }

        if (compA.Owner != null)
            _popup.PopupEntity(Loc.GetString("trade-terminal-completed"), uidA, compA.Owner.Value, PopupType.Large);

        if (compB.Owner != null)
            _popup.PopupEntity(Loc.GetString("trade-terminal-completed"), uidB, compB.Owner.Value, PopupType.Large);

        RaiseLocalEvent(new TradeExecutedEvent(uidA, uidB));
        UpdateBothUi(uidA, compA);
        TryCleanupCompletedPair(uidA, compA);
    }

    private void ResetTerminal(EntityUid uid, TradeTerminalComponent comp)
    {
        if (comp.Owner != null)
            _activeUsers.Remove(comp.Owner.Value);

        ClearOfferSlots(comp);
        comp.Owner = null;
        comp.LinkedTerminal = null;
        comp.CountdownEndTime = TimeSpan.Zero;
        comp.ConfirmedBy = null;
        comp.HasConfirmed = false;
        comp.NextRingTime = TimeSpan.Zero;
        comp.CallTimeoutTime = TimeSpan.Zero;
        comp.CompletedExpireTime = TimeSpan.Zero;

        SetState(uid, comp, TradeSessionState.Idle);

        RemComp<JitteringComponent>(uid);

        if (TryComp<TelephoneComponent>(uid, out var phone))
            _telephone.TerminateTelephoneCalls(new Entity<TelephoneComponent>(uid, phone));

        _ui.CloseUi(uid, TradeUiKey.Key);
        UpdateUi(uid, comp);
    }
}
