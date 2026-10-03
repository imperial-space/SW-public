using System.Diagnostics.CodeAnalysis;
using Content.Server.Imperial.Medieval.Trading;
using Content.Server.Stack;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Imperial.Medieval.Salary;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Salary;

public sealed class MedievalSalarySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TradingItemDeliverySystem _delivery = default!;

    /// <summary>
    /// Default and current salary of every role type seen this round.
    /// </summary>
    private readonly Dictionary<string, (int Default, int Amount)> _roles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSalaryReceiverComponent, MapInitEvent>(OnReceiverMapInit);
        SubscribeLocalEvent<MedievalAtmComponent, ActivateInWorldEvent>(OnAtmActivate);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _roles.Clear());

        SubscribeNetworkEvent<RequestSalaryStateMessage>(OnRequestState);
        SubscribeNetworkEvent<SetRoleSalaryMessage>(OnSetRoleSalary);
        SubscribeNetworkEvent<SetPersonalSalaryMessage>(OnSetPersonalSalary);
    }

    private void OnReceiverMapInit(Entity<MedievalSalaryReceiverComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextPayment = _timing.CurTime + ent.Comp.ReceiveTimer;

        if (ent.Comp.RoleType == string.Empty)
            return;

        if (_roles.TryGetValue(ent.Comp.RoleType, out var role))
            ent.Comp.PaymentAmount = role.Amount;
        else
            _roles[ent.Comp.RoleType] = (ent.Comp.PaymentAmount, ent.Comp.PaymentAmount);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        Dictionary<ProtoId<MedievalFactionPrototype>, float>? factors = null;

        var query = EntityQueryEnumerator<MedievalSalaryReceiverComponent>();
        while (query.MoveNext(out var uid, out var receiver))
        {
            if (!IsActive(uid))
            {
                receiver.NextPayment += TimeSpan.FromSeconds(frameTime);
                continue;
            }

            if (curTime < receiver.NextPayment)
                continue;

            receiver.Balance += GetPayout(uid, receiver, factors ??= new());
            receiver.NextPayment = curTime + receiver.ReceiveTimer;
        }
    }

    private bool IsActive(EntityUid uid)
    {
        return !_mobState.IsDead(uid) && !(TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD);
    }

    private int GetDefault(MedievalSalaryReceiverComponent receiver)
    {
        return _roles.TryGetValue(receiver.RoleType, out var role) ? role.Default : receiver.PaymentAmount;
    }

    /// <summary>
    /// When the treasury no longer covers the allocated salaries, raises above the default are scaled down.
    /// </summary>
    private int GetPayout(EntityUid uid, MedievalSalaryReceiverComponent receiver, Dictionary<ProtoId<MedievalFactionPrototype>, float> factors)
    {
        var defaultPayment = GetDefault(receiver);
        var raise = receiver.PaymentAmount - defaultPayment;
        if (raise <= 0 || !TryComp<MedievalFactionMemberComponent>(uid, out var member))
            return receiver.PaymentAmount;

        if (!factors.TryGetValue(member.Faction, out var factor))
        {
            var (treasury, allocated) = GetTreasury(member.Faction);
            var raises = 0;
            foreach (var (_, other) in GetActiveReceivers(member.Faction))
            {
                raises += Math.Max(0, other.PaymentAmount - GetDefault(other));
            }

            factor = allocated <= treasury || raises == 0 ? 1f : Math.Max(0f, 1f - (float) (allocated - treasury) / raises);
            factors[member.Faction] = factor;
        }

        return defaultPayment + (int) MathF.Floor(raise * factor);
    }

    private List<(EntityUid Uid, MedievalSalaryReceiverComponent Receiver)> GetReceivers(ProtoId<MedievalFactionPrototype> faction)
    {
        var receivers = new List<(EntityUid, MedievalSalaryReceiverComponent)>();
        var query = EntityQueryEnumerator<MedievalSalaryReceiverComponent, MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var receiver, out var member))
        {
            if (member.Faction == faction)
                receivers.Add((uid, receiver));
        }

        return receivers;
    }

    private IEnumerable<(EntityUid Uid, MedievalSalaryReceiverComponent Receiver)> GetActiveReceivers(ProtoId<MedievalFactionPrototype> faction)
    {
        foreach (var entry in GetReceivers(faction))
        {
            if (IsActive(entry.Uid))
                yield return entry;
        }
    }

    private (int Treasury, int Allocated) GetTreasury(ProtoId<MedievalFactionPrototype> faction)
    {
        var defaults = 0;
        var allocated = 0;
        foreach (var (_, receiver) in GetActiveReceivers(faction))
        {
            defaults += GetDefault(receiver);
            allocated += receiver.PaymentAmount;
        }

        return ((int) MathF.Floor(defaults * (1f + MedievalSalaryManagerComponent.TreasuryMargin)), allocated);
    }

    private bool TryGetManager(ICommonSession session, [NotNullWhen(true)] out EntityUid? manager, out ProtoId<MedievalFactionPrototype> faction)
    {
        manager = session.AttachedEntity;
        faction = default;

        if (manager == null ||
            !HasComp<MedievalSalaryManagerComponent>(manager) ||
            !TryComp<MedievalFactionMemberComponent>(manager, out var member) ||
            !IsActive(manager.Value))
        {
            return false;
        }

        faction = member.Faction;
        return true;
    }

    private bool CanAfford(EntityUid manager, ProtoId<MedievalFactionPrototype> faction, int delta)
    {
        if (delta <= 0)
            return true;

        var (treasury, allocated) = GetTreasury(faction);
        if (allocated + delta <= treasury)
            return true;

        _popup.PopupEntity(Loc.GetString("medieval-salary-over-budget"), manager, manager);
        return false;
    }

    private void OnRequestState(RequestSalaryStateMessage msg, EntitySessionEventArgs args)
    {
        if (TryGetManager(args.SenderSession, out _, out var faction))
            SendState(args.SenderSession, faction);
    }

    private void OnSetRoleSalary(SetRoleSalaryMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetManager(args.SenderSession, out var manager, out var faction) ||
            !_roles.TryGetValue(msg.RoleType, out var role))
        {
            return;
        }

        var affected = GetReceivers(faction).FindAll(x => x.Receiver.RoleType == msg.RoleType && !x.Receiver.PersonallyModified);
        if (affected.Count == 0)
            return;

        var amount = Math.Clamp(msg.Amount, MedievalSalaryManagerComponent.GetMin(role.Default), MedievalSalaryManagerComponent.GetMax(role.Default));
        var delta = 0;
        foreach (var (uid, receiver) in affected)
        {
            if (IsActive(uid))
                delta += amount - receiver.PaymentAmount;
        }

        if (!CanAfford(manager.Value, faction, delta))
            return;

        _roles[msg.RoleType] = (role.Default, amount);
        foreach (var (_, receiver) in affected)
        {
            receiver.PaymentAmount = amount;
        }

        SendState(args.SenderSession, faction);
    }

    private void OnSetPersonalSalary(SetPersonalSalaryMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetManager(args.SenderSession, out var manager, out var faction) ||
            !TryGetEntity(msg.Target, out var target) ||
            !TryComp<MedievalSalaryReceiverComponent>(target, out var receiver) ||
            !TryComp<MedievalFactionMemberComponent>(target, out var member) ||
            member.Faction != faction ||
            !_roles.TryGetValue(receiver.RoleType, out var role))
        {
            return;
        }

        var amount = msg.Amount is { } requested
            ? Math.Clamp(requested, MedievalSalaryManagerComponent.GetMin(role.Default), MedievalSalaryManagerComponent.GetMax(role.Default))
            : role.Amount;

        var delta = IsActive(target.Value) ? amount - receiver.PaymentAmount : 0;
        if (!CanAfford(manager.Value, faction, delta))
            return;

        receiver.PaymentAmount = amount;
        receiver.PersonallyModified = msg.Amount != null;

        SendState(args.SenderSession, faction);
    }

    private void SendState(ICommonSession session, ProtoId<MedievalFactionPrototype> faction)
    {
        var roles = new Dictionary<string, SalaryRoleData>();
        var members = new List<SalaryMemberData>();

        foreach (var (uid, receiver) in GetReceivers(faction))
        {
            if (!_roles.TryGetValue(receiver.RoleType, out var role))
                continue;

            roles[receiver.RoleType] = new SalaryRoleData(receiver.RoleType, role.Default, role.Amount);
            members.Add(new SalaryMemberData(GetNetEntity(uid), Name(uid), receiver.RoleType, receiver.PaymentAmount, receiver.PersonallyModified));
        }

        var (treasury, allocated) = GetTreasury(faction);
        RaiseNetworkEvent(new SalaryStateMessage(treasury, allocated, new(roles.Values), members), session);
    }

    private void OnAtmActivate(Entity<MedievalAtmComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;

        if (!TryComp<MedievalSalaryReceiverComponent>(args.User, out var receiver) || receiver.Balance <= 0)
        {
            _popup.PopupEntity(Loc.GetString("medieval-atm-empty"), ent, args.User);
            return;
        }

        var amount = receiver.Balance;
        receiver.Balance = 0;

        foreach (var cash in _stack.SpawnMultiple(ent.Comp.Cash.Id, amount, Transform(args.User).Coordinates))
        {
            _delivery.Deliver(cash, args.User);
        }

        _audio.PlayPvs(ent.Comp.WithdrawSound, ent);
        _popup.PopupEntity(Loc.GetString("medieval-atm-withdraw", ("amount", amount)), ent, args.User);
    }
}
