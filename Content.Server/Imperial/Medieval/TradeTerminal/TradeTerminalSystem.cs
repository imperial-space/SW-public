using System.Linq;
using Content.Server.Telephone;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Telephone;
using Content.Shared.Trade;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Trade;

public sealed class TradeTerminalSystem : SharedTradeTerminalSystem
{
    private static readonly SoundPathSpecifier RingSound =
        new("/Audio/Machines/double_ring.ogg");

    private readonly Dictionary<EntityUid, EntityUid> _activeUsers = new();

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TelephoneSystem _telephone = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TradeTerminalComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<TradeTerminalComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        SubscribeLocalEvent<TradeTerminalComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<TradeTerminalComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<TradeTerminalComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<TradeTerminalComponent, EntRemovedFromContainerMessage>(OnItemRemoved);

        SubscribeLocalEvent<TradeTerminalComponent, TradeCallMessage>(OnCall);
        SubscribeLocalEvent<TradeTerminalComponent, TradeAcceptCallMessage>(OnAcceptCall);
        SubscribeLocalEvent<TradeTerminalComponent, TradeHangUpMessage>(OnHangUp);
        SubscribeLocalEvent<TradeTerminalComponent, TradeConfirmMessage>(OnConfirm);
        SubscribeLocalEvent<TradeTerminalComponent, TradeCancelMessage>(OnCancel);
        SubscribeLocalEvent<TradeTerminalComponent, TradeRemoveItemMessage>(OnBuiRemoveItem);

        SubscribeLocalEvent<TradeTerminalComponent, BoundUIClosedEvent>(OnUIClosed);

        SubscribeLocalEvent<ActorComponent, DamageChangedEvent>(OnPlayerDamaged);
        SubscribeLocalEvent<ActorComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TradeTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State == TradeSessionState.Ringing &&
                _timing.CurTime >= comp.NextRingTime)
            {
                _audio.PlayPvs(RingSound, uid);
                comp.NextRingTime = _timing.CurTime + TimeSpan.FromSeconds(2.5);
            }

            if (comp.State != TradeSessionState.Countdown)
                continue;
            if (comp.LinkedTerminal is not { } partnerId)
                continue;
            if (!TryComp<TradeTerminalComponent>(partnerId, out var partner))
                continue;

            // Ждем, когда текущее время догонит время окончания ритуала
            if (_timing.CurTime >= comp.CountdownEndTime)
            {
                ExecuteTrade(uid, comp, partnerId, partner);
            }
        }
    }

    private void OnActivate(EntityUid uid, TradeTerminalComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (comp.Owner != null && comp.Owner != args.User)
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-busy"), uid, args.User);
            return;
        }

        if (comp.Owner == null)
        {
            comp.Owner = args.User;
            _activeUsers[args.User] = uid;
        }

        _ui.OpenUi(uid, TradeUiKey.Key, actor.PlayerSession);
        UpdateUi(uid, comp);
        args.Handled = true;
    }

    private void OnGetVerbs(EntityUid uid, TradeTerminalComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if (!TryComp<ActorComponent>(args.User, out _))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("trade-terminal-verb-open"),
            Priority = 1,
            Act = () =>
            {
                if (!TryComp<ActorComponent>(args.User, out var actor))
                    return;

                if (comp.Owner != null && comp.Owner != args.User)
                {
                    _popup.PopupEntity(Loc.GetString("trade-terminal-busy"), uid, args.User);
                    return;
                }

                if (comp.Owner == null)
                {
                    comp.Owner = args.User;
                    _activeUsers[args.User] = uid;
                }

                _ui.OpenUi(uid, TradeUiKey.Key, actor.PlayerSession);
                UpdateUi(uid, comp);
            },
        });
    }

    private void OnInsertAttempt(EntityUid uid, TradeTerminalComponent comp, ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;
        if (comp.State is not TradeSessionState.Active)
            args.Cancel();
    }

    private void OnRemoveAttempt(EntityUid uid, TradeTerminalComponent comp, ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;
        if (comp.State is TradeSessionState.Countdown or TradeSessionState.Completed)
            args.Cancel();
    }

    private void OnUIClosed(EntityUid uid, TradeTerminalComponent comp, BoundUIClosedEvent args)
    {
        if (!args.UiKey.Equals(TradeUiKey.Key))
            return;

        if (comp.Owner == args.Actor)
        {
            _activeUsers.Remove(args.Actor);

            if (comp.State != TradeSessionState.Idle)
                HangUp(uid, comp);
            else
                comp.Owner = null;
        }
    }

    private void ResetConfirmations(TradeTerminalComponent comp)
    {
        comp.HasConfirmed = false;

        if (comp.LinkedTerminal is { } partnerId &&
            TryComp<TradeTerminalComponent>(partnerId, out var partner))
        {
            if (partner.HasConfirmed)
            {
                partner.HasConfirmed = false;
                if (partner.Owner != null)
                {
                    _popup.PopupEntity(
                        Loc.GetString("trade-terminal-offer-changed"),
                        partnerId,
                        partner.Owner.Value);
                }
            }
        }
    }

    private void OnItemInserted(EntityUid uid, TradeTerminalComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        ResetConfirmations(comp);

        UpdateBothUi(uid, comp);
        DirtyAppearance(uid, comp);
    }

    private void OnItemRemoved(EntityUid uid, TradeTerminalComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        ResetConfirmations(comp);

        UpdateBothUi(uid, comp);
        DirtyAppearance(uid, comp);
    }

    private void OnCall(EntityUid uid, TradeTerminalComponent comp, TradeCallMessage args)
    {
        if (comp.State != TradeSessionState.Idle)
            return;

        var targetUid = GetEntity(args.Target);
        if (!TryComp<TradeTerminalComponent>(targetUid, out var target) || targetUid == uid)
            return;

        if (!IsAvailable(target))
        {
            if (comp.Owner != null)
                _popup.PopupEntity(Loc.GetString("trade-terminal-target-busy"), uid, comp.Owner.Value);
            return;
        }

        comp.LinkedTerminal = targetUid;
        comp.State = TradeSessionState.Calling;

        target.LinkedTerminal = uid;
        target.State = TradeSessionState.Ringing;
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
        if (comp.State != TradeSessionState.Ringing)
            return;
        if (comp.LinkedTerminal is not { } partnerId)
            return;
        if (!TryComp<TradeTerminalComponent>(partnerId, out var partner))
            return;

        comp.State = TradeSessionState.Active;
        partner.State = TradeSessionState.Active;

        RemComp<JitteringComponent>(uid);

        if (comp.Owner != null)
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-connected", ("partner", TerminalName(partnerId))),
                uid,
                comp.Owner.Value);
        }

        if (partner.Owner != null)
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-connected", ("partner", TerminalName(uid))),
                partnerId,
                partner.Owner.Value);
        }

        if (TryComp<TelephoneComponent>(uid, out var phoneReceiver) &&
            TryComp<TelephoneComponent>(partnerId, out var phoneCaller))
        {
            var callerEntity = new Entity<TelephoneComponent>(partnerId, phoneCaller);
            var receiverEntity = new Entity<TelephoneComponent>(uid, phoneReceiver);
            var caller = comp.Owner ?? partnerId;

            _telephone.CallTelephone(callerEntity,
                receiverEntity,
                caller,
                new TelephoneCallOptions { ForceConnect = true, IgnoreRange = true });
        }

        UpdateBothUi(uid, comp);
    }

    private void OnHangUp(EntityUid uid, TradeTerminalComponent comp, TradeHangUpMessage args)
    {
        HangUp(uid, comp);
    }

    private void HangUp(EntityUid uid, TradeTerminalComponent comp)
    {
        if (comp.LinkedTerminal is { } partnerId &&
            TryComp<TradeTerminalComponent>(partnerId, out var partner))
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
        RaiseLocalEvent(new TradeCancelledEvent(uid));
    }

    private void OnConfirm(EntityUid uid, TradeTerminalComponent comp, TradeConfirmMessage args)
    {
        if (comp.State != TradeSessionState.Active)
            return;
        if (comp.LinkedTerminal is not { } partnerId)
            return;
        if (!TryComp<TradeTerminalComponent>(partnerId, out var partner))
            return;

        comp.HasConfirmed = !comp.HasConfirmed;

        if (comp.HasConfirmed && partner.HasConfirmed)
        {
            var confirmerNet = comp.Owner != null ? GetNetEntity(comp.Owner.Value) : (NetEntity?)null;
            var confirmerName = comp.Owner != null ? Name(comp.Owner.Value) : "???";

            var endTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CountdownDuration);

            comp.State = TradeSessionState.Countdown;
            comp.CountdownEndTime = endTime;
            comp.ConfirmedBy = confirmerNet;
            comp.HasConfirmed = false;

            partner.State = TradeSessionState.Countdown;
            partner.CountdownEndTime = endTime;
            partner.ConfirmedBy = confirmerNet;
            partner.HasConfirmed = false;

            if (comp.Owner != null)
            {
                _popup.PopupEntity(
                    Loc.GetString("trade-terminal-countdown-self", ("seconds", (int)comp.CountdownDuration)),
                    uid,
                    comp.Owner.Value);
            }

            if (partner.Owner != null)
            {
                _popup.PopupEntity(
                    Loc.GetString("trade-terminal-countdown-partner",
                        ("name", confirmerName),
                        ("seconds", (int)comp.CountdownDuration)),
                    partnerId,
                    partner.Owner.Value,
                    PopupType.Medium);
            }
        }

        UpdateBothUi(uid, comp);
    }

    private void OnCancel(EntityUid uid, TradeTerminalComponent comp, TradeCancelMessage args)
    {
        switch (comp.State)
        {
            case TradeSessionState.Countdown:
                CancelCountdown(uid, comp);
                break;
            default:
                HangUp(uid, comp);
                break;
        }
    }

    private void CancelCountdown(EntityUid uid, TradeTerminalComponent comp)
    {
        if (comp.LinkedTerminal is not { } partnerId)
            return;
        if (!TryComp<TradeTerminalComponent>(partnerId, out var partner))
            return;

        var cancellerName = comp.Owner != null ? Name(comp.Owner.Value) : "???";

        comp.State = TradeSessionState.Active;
        comp.CountdownEndTime = TimeSpan.Zero;
        comp.ConfirmedBy = null;
        comp.HasConfirmed = false;

        partner.State = TradeSessionState.Active;
        partner.CountdownEndTime = TimeSpan.Zero;
        partner.ConfirmedBy = null;
        partner.HasConfirmed = false;

        if (comp.Owner != null)
            _popup.PopupEntity(Loc.GetString("trade-terminal-countdown-cancelled"), uid, comp.Owner.Value);

        if (partner.Owner != null)
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-countdown-cancelled-partner", ("name", cancellerName)),
                partnerId,
                partner.Owner.Value);
        }

        UpdateBothUi(uid, comp);
    }

    private void OnBuiRemoveItem(EntityUid uid, TradeTerminalComponent comp, TradeRemoveItemMessage args)
    {
        if (comp.State is TradeSessionState.Countdown or TradeSessionState.Completed || comp.HasConfirmed)
        {
            if (comp.Owner != null)
                _popup.PopupEntity(Loc.GetString("trade-terminal-locked"), uid, comp.Owner.Value);
            return;
        }

        var item = GetEntity(args.Item);
        var container = GetOfferContainer(uid, comp);

        if (!container.Contains(item))
            return;

        if (comp.Owner != null)
        {
            if (!_hands.TryPickupAnyHand(comp.Owner.Value, item))
            {
                Containers.Remove(item, container, force: true);
                _transform.AttachToGridOrMap(item);
            }
        }
        else
        {
            Containers.Remove(item, container, force: true);
            _transform.AttachToGridOrMap(item);
        }
    }

    private void ExecuteTrade(EntityUid uidA,
        TradeTerminalComponent compA,
        EntityUid uidB,
        TradeTerminalComponent compB)
    {
        if (compA.State != TradeSessionState.Countdown)
            return;

        compA.State = TradeSessionState.Completed;
        compB.State = TradeSessionState.Completed;

        var cA = GetOfferContainer(uidA, compA);
        var cB = GetOfferContainer(uidB, compB);

        var itemsA = cA.ContainedEntities.ToList();
        var itemsB = cB.ContainedEntities.ToList();

        foreach (var i in itemsA)
        {
            Containers.Remove(i, cA, force: true);
        }

        foreach (var i in itemsB)
        {
            Containers.Remove(i, cB, force: true);
        }

        foreach (var i in itemsB)
        {
            Containers.Insert(i, cA, force: true);
        }

        foreach (var i in itemsA)
        {
            Containers.Insert(i, cB, force: true);
        }

        if (compA.Owner != null)
            _popup.PopupEntity(Loc.GetString("trade-terminal-completed"), uidA, compA.Owner.Value, PopupType.Large);

        if (compB.Owner != null)
            _popup.PopupEntity(Loc.GetString("trade-terminal-completed"), uidB, compB.Owner.Value, PopupType.Large);

        RaiseLocalEvent(new TradeExecutedEvent(uidA, uidB));
        UpdateBothUi(uidA, compA);

        Timer.Spawn(TimeSpan.FromSeconds(5),
            () =>
            {
                if (TryComp<TradeTerminalComponent>(uidA, out var a))
                    ResetTerminal(uidA, a);
                if (TryComp<TradeTerminalComponent>(uidB, out var b))
                    ResetTerminal(uidB, b);
            });
    }

    private void ReturnItemsToWorld(EntityUid uid, TradeTerminalComponent comp)
    {
        var container = GetOfferContainer(uid, comp);
        foreach (var item in container.ContainedEntities.ToList())
        {
            Containers.Remove(item, container, force: true);
            _transform.AttachToGridOrMap(item);
        }
    }

    private void ResetTerminal(EntityUid uid, TradeTerminalComponent comp)
    {
        if (comp.Owner != null)
            _activeUsers.Remove(comp.Owner.Value);

        comp.State = TradeSessionState.Idle;
        comp.Owner = null;
        comp.LinkedTerminal = null;
        comp.CountdownEndTime = TimeSpan.Zero;
        comp.ConfirmedBy = null;
        comp.HasConfirmed = false;
        comp.NextRingTime = TimeSpan.Zero;

        RemComp<JitteringComponent>(uid);

        if (TryComp<TelephoneComponent>(uid, out var phone))
            _telephone.TerminateTelephoneCalls(new Entity<TelephoneComponent>(uid, phone));

        DirtyAppearance(uid, comp);
        _ui.CloseUi(uid, TradeUiKey.Key);
        UpdateUi(uid, comp);
    }

    private void DirtyAppearance(EntityUid uid, TradeTerminalComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;
        _appearance.SetData(uid, TradeTerminalVisuals.State, comp.State, appearance);
    }

    private string TerminalName(EntityUid uid)
    {
        return MetaData(uid).EntityName;
    }

    private void UpdateBothUi(EntityUid uid, TradeTerminalComponent comp)
    {
        UpdateUi(uid, comp);

        if (comp.LinkedTerminal is { } partnerId &&
            TryComp<TradeTerminalComponent>(partnerId, out var partner))
            UpdateUi(partnerId, partner);
    }

    private void UpdateUi(EntityUid uid, TradeTerminalComponent comp)
    {
        string? partnerName = null;
        TradeSessionState? partnerState = null;
        List<TradeItemDto>? partnerItems = null;
        string? incomingCallerName = null;

        var partnerConfirmed = false;

        if (comp.LinkedTerminal is { } partnerId &&
            TryComp<TradeTerminalComponent>(partnerId, out var partner))
        {
            partnerName = TerminalName(partnerId);
            partnerState = partner.State;
            partnerConfirmed = partner.HasConfirmed;

            if (comp.State is TradeSessionState.Active
                or TradeSessionState.Countdown
                or TradeSessionState.Completed)
                partnerItems = MakeItemList(partnerId, partner);

            if (comp.State == TradeSessionState.Ringing)
                incomingCallerName = TerminalName(partnerId);
        }

        string? confirmedByName = null;
        if (comp.ConfirmedBy is { } confirmedNet)
        {
            var confirmedUid = GetEntity(confirmedNet);
            confirmedByName = Name(confirmedUid);
        }

        var state = new TradeBuiState(
            comp.State,
            TerminalName(uid),
            MakeItemList(uid, comp),
            partnerName,
            partnerState,
            partnerItems,
            incomingCallerName,
            comp.CountdownEndTime,
            comp.CountdownDuration,
            confirmedByName,
            MakeDirectory(uid),
            comp.HasConfirmed,
            partnerConfirmed
        );

        _ui.SetUiState(uid, TradeUiKey.Key, state);
    }

    private List<TradeItemDto> MakeItemList(EntityUid uid, TradeTerminalComponent comp)
    {
        var container = GetOfferContainer(uid, comp);
        var list = new List<TradeItemDto>(container.ContainedEntities.Count);

        foreach (var item in container.ContainedEntities)
        {
            var meta = MetaData(item);
            int? stackCount = null;
            if (TryComp<StackComponent>(item, out var stack))
                stackCount = stack.Count;

            list.Add(new TradeItemDto(
                GetNetEntity(item),
                meta.EntityName,
                meta.EntityDescription,
                stackCount));
        }

        return list;
    }

    private List<TradeTerminalDto> MakeDirectory(EntityUid excludeUid)
    {
        var list = new List<TradeTerminalDto>();
        var query = EntityQueryEnumerator<TradeTerminalComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid == excludeUid)
                continue;

            list.Add(new TradeTerminalDto(
                GetNetEntity(uid),
                MetaData(uid).EntityName,
                comp.State));
        }

        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return list;
    }

    private void OnPlayerDamaged(EntityUid uid, ActorComponent component, DamageChangedEvent args)
    {
        if (args.DamageIncreased && _activeUsers.TryGetValue(uid, out var terminalUid))
        {
            if (TryComp<TradeTerminalComponent>(terminalUid, out var comp))
                ForceCloseAndHangUp(terminalUid, comp, uid);
        }
    }

    private void OnMobStateChanged(EntityUid uid, ActorComponent component, MobStateChangedEvent args)
    {
        if ((args.NewMobState == MobState.Critical || args.NewMobState == MobState.Dead) &&
            _activeUsers.TryGetValue(uid, out var terminalUid))
        {
            if (TryComp<TradeTerminalComponent>(terminalUid, out var comp))
                ForceCloseAndHangUp(terminalUid, comp, uid);
        }
    }

    private void ForceCloseAndHangUp(EntityUid uid, TradeTerminalComponent comp, EntityUid userUid)
    {
        _activeUsers.Remove(userUid);

        if (comp.State != TradeSessionState.Idle)
            HangUp(uid, comp);
        else
            comp.Owner = null;

        _ui.CloseUi(uid, TradeUiKey.Key);
    }
}
