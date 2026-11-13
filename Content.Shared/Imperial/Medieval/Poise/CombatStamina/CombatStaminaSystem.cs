using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.Damage.Events;

namespace Content.Shared.Imperial.Medieval.Poise.CombatStamina;

/// <summary>
/// Server-authoritative refundable expenditure, with prediction for the owning player.
/// Ordinary stamina remains the source of truth for available stamina and critical state.
/// </summary>
public sealed class CombatStaminaSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;

    private enum ChangeKind : byte { Sprint, Parry, Refund }
    private readonly record struct ChangeContext(ChangeKind Kind, float Refund = 0f);
    private readonly Dictionary<EntityUid, ChangeContext> _contexts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoiseComponent, ComponentStartup>(OnPoiseStartup);
        SubscribeLocalEvent<PoiseComponent, StaminaCostCheckEvent>(OnCostCheck);
        SubscribeLocalEvent<PoiseComponent, StaminaCostPaidEvent>(OnCostPaid);
        SubscribeLocalEvent<PoiseComponent, StaminaFinalChangeEvent>(OnFinalChange);
        SubscribeLocalEvent<PoiseComponent, StaminaNaturalRegenAttemptEvent>(OnNaturalRegen);
        SubscribeLocalEvent<PoiseComponent, StaminaDamageQueryEvent>(OnQuery);
        SubscribeLocalEvent<PoiseComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private bool Simulates(EntityUid uid) => _net.IsServer || _players.LocalEntity == uid;

    private bool Eligible(EntityUid uid, PoiseComponent poise)
        => !poise.Npc && !poise.PoiseBroken && PoiseSystem.GetEffectivePoiseThreshold(poise) > 0 &&
           TryComp<CombatModeComponent>(uid, out var combat) && combat.IsInCombatMode;

    private void OnPoiseStartup(Entity<PoiseComponent> entity, ref ComponentStartup args)
    {
        if (_net.IsServer && !entity.Comp.Npc && HasComp<StaminaComponent>(entity.Owner))
            EnsureComp<CombatStaminaComponent>(entity.Owner);
    }

    private void OnCostCheck(EntityUid uid, PoiseComponent poise, ref StaminaCostCheckEvent args)
    {
        if (!Simulates(uid))
            return;
        Advance(uid);
        if (!_contexts.TryGetValue(uid, out var context) || context.Kind != ChangeKind.Sprint ||
            !TryComp<CombatStaminaComponent>(uid, out var combat))
            return;

        // Match the final-change hook's actual units before checking affordability.
        var cost = args.Cost * _stamina.UniversalStaminaDamageModifier;
        args.CheckCost = CombatStaminaMath.SplitCost(GetPending(uid), cost, combat.SprintMultiplier).NormalCost;
    }

    private void OnCostPaid(EntityUid uid, PoiseComponent poise, ref StaminaCostPaidEvent args)
    {
        if (!Simulates(uid) || _contexts.ContainsKey(uid) || !Eligible(uid, poise) ||
            !float.IsFinite(args.ActualCost) || args.ActualCost <= 0f ||
            !TryComp<StaminaComponent>(uid, out var stamina) || stamina.Critical)
            return;

        var combat = EnsureComp<CombatStaminaComponent>(uid);
        var fraction = GetRefundFraction(uid, combat);
        var added = args.ActualCost * fraction;
        if (added <= 0f)
            return;

        if (combat.Pending <= CombatStaminaMath.Epsilon)
        {
            combat.Pending = 0f;
            combat.RecoveryStarts = _timing.CurTime + combat.RecoveryDelay;
            combat.LastUpdate = _timing.CurTime;
        }

        combat.Pending = MathF.Min(stamina.StaminaDamage, combat.Pending + added);
        Dirty(uid, combat);
    }

    private float GetRefundFraction(EntityUid uid, CombatStaminaComponent combat)
    {
        var fraction = combat.LightRefund;
        if (TryComp<InventoryComponent>(uid, out var inventory))
        {
            var slots = _inventory.GetSlotEnumerator((uid, inventory), SlotFlags.OUTERCLOTHING);
            while (slots.NextItem(out var item))
            {
                if (!TryComp<PoiseArmorComponent>(item, out var armor) || !armor.ProvidesArmorBonuses)
                    continue;
                fraction = armor.ArmorClass switch
                {
                    PoiseArmorClass.Medium => combat.MediumRefund,
                    PoiseArmorClass.Heavy => combat.HeavyRefund,
                    _ => combat.LightRefund,
                };
                break;
            }
        }

        return float.IsFinite(fraction) ? Math.Clamp(fraction, 0f, 1f) : 0f;
    }

    private void OnFinalChange(EntityUid uid, PoiseComponent poise, ref StaminaFinalChangeEvent args)
    {
        if (!Simulates(uid))
            return;

        if (_contexts.TryGetValue(uid, out var context))
        {
            if (context.Kind == ChangeKind.Refund)
            {
                // Refunds are fixed actual units, unaffected by damage/regen multipliers.
                args.Value = -context.Refund;
                return;
            }

            if (args.Value > 0f && TryComp<CombatStaminaComponent>(uid, out var combat))
            {
                var multiplier = context.Kind == ChangeKind.Sprint ? combat.SprintMultiplier : 1f;
                var split = CombatStaminaMath.SplitCost(GetPending(uid), args.Value, multiplier);
                args.Value = split.NormalCost;
                SetPending(uid, combat, combat.Pending - split.CombatSpent);
            }
        }

        // External healing may restore non-refundable expenditure first. Never keep
        // a pending refund larger than the actual remaining stamina deficit.
        if (args.Value < 0f && TryComp<CombatStaminaComponent>(uid, out var pending) &&
            TryComp<StaminaComponent>(uid, out var stamina))
            SetPending(uid, pending, MathF.Min(pending.Pending, MathF.Max(0f, stamina.StaminaDamage + args.Value)));
    }

    private void OnNaturalRegen(EntityUid uid, PoiseComponent poise, ref StaminaNaturalRegenAttemptEvent args)
    {
        if (GetPending(uid) > CombatStaminaMath.Epsilon)
            args.Cancelled = true;
    }

    private void OnQuery(EntityUid uid, PoiseComponent poise, ref StaminaDamageQueryEvent args)
    {
        if (GetPending(uid) > CombatStaminaMath.Epsilon && TryComp<StaminaComponent>(uid, out var stamina))
            args.Value = stamina.StaminaDamage;
    }

    private void OnRejuvenate(Entity<PoiseComponent> entity, ref RejuvenateEvent args) => Clear(entity.Owner);

    public float GetPending(EntityUid uid)
    {
        if (!TryComp<CombatStaminaComponent>(uid, out var combat) ||
            !TryComp<StaminaComponent>(uid, out var stamina) || !float.IsFinite(combat.Pending))
            return 0f;
        return Math.Clamp(combat.Pending, 0f, MathF.Max(0f, stamina.StaminaDamage));
    }

    public bool CanFundSprint(EntityUid uid)
        => TryComp<PoiseComponent>(uid, out var poise) && Eligible(uid, poise) &&
           TryComp<StaminaComponent>(uid, out var stamina) && !stamina.Critical &&
           GetPending(uid) > CombatStaminaMath.Epsilon;

    public bool TrySpendSprint(EntityUid uid, float cost)
    {
        if (!float.IsFinite(cost) || cost < 0f)
            return false;
        var previous = PushContext(uid, new ChangeContext(ChangeKind.Sprint));
        try
        {
            return _stamina.TryTakeStamina(uid, cost, ignoreResist: true, log: false);
        }
        finally
        {
            RestoreContext(uid, previous);
        }
    }

    /// <summary>Only parry penalties consume the pool at 1:1; other incoming hits do not.</summary>
    public void ApplyParryStaminaDamage(EntityUid uid, float damage)
    {
        if (!float.IsFinite(damage) || damage < 0f)
            return;
        Advance(uid);
        var previous = PushContext(uid, new ChangeContext(ChangeKind.Parry));
        try
        {
            // Preserve the existing parry's resistance behavior and lack of a weapon
            // origin: it must bypass poise protection and cannot itself be parried.
            _stamina.TakeStaminaDamage(uid, damage);
        }
        finally
        {
            RestoreContext(uid, previous);
        }
    }

    public void OnPoiseBroken(EntityUid uid)
    {
        if (!Simulates(uid) || !TryComp<CombatStaminaComponent>(uid, out var combat))
            return;
        // PoiseSystem invokes this just before flipping the broken flag. Settle any
        // recovery already due, then refund half of the remaining balance once.
        Advance(uid);
        var fraction = float.IsFinite(combat.PoiseBreakRefund) ? Math.Clamp(combat.PoiseBreakRefund, 0f, 1f) : 0f;
        var refund = GetPending(uid) * fraction;
        Clear(uid);
        RestoreStamina(uid, refund);
    }

    public void Clear(EntityUid uid)
    {
        if (!Simulates(uid) || !TryComp<CombatStaminaComponent>(uid, out var combat))
            return;
        SetPending(uid, combat, 0f);
    }

    private void SetPending(EntityUid uid, CombatStaminaComponent combat, float value)
    {
        combat.Pending = float.IsFinite(value) ? MathF.Max(0f, value) : 0f;
        if (combat.Pending <= CombatStaminaMath.Epsilon)
        {
            combat.Pending = 0f;
            combat.RecoveryStarts = TimeSpan.Zero;
            combat.LastUpdate = TimeSpan.Zero;
        }
        Dirty(uid, combat);
    }

    private float RestoreStamina(EntityUid uid, float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f || !TryComp<StaminaComponent>(uid, out var stamina) || stamina.Critical)
            return 0f;
        amount = MathF.Min(amount, stamina.StaminaDamage);
        var before = stamina.StaminaDamage;
        var previous = PushContext(uid, new ChangeContext(ChangeKind.Refund, amount));
        try
        {
            _stamina.TakeStaminaDamage(uid, -amount, stamina, visual: false, ignoreResist: true, log: false);
        }
        finally
        {
            RestoreContext(uid, previous);
        }
        return MathF.Max(0f, before - stamina.StaminaDamage);
    }

    private ChangeContext? PushContext(EntityUid uid, ChangeContext context)
    {
        ChangeContext? previous = _contexts.TryGetValue(uid, out var old) ? old : null;
        _contexts[uid] = context;
        return previous;
    }

    private void RestoreContext(EntityUid uid, ChangeContext? previous)
    {
        if (previous is { } context)
            _contexts[uid] = context;
        else
            _contexts.Remove(uid);
    }

    private void Advance(EntityUid uid)
    {
        if (!Simulates(uid) || !TryComp<CombatStaminaComponent>(uid, out var combat) || combat.Pending <= 0f ||
            !TryComp<PoiseComponent>(uid, out var poise) || !TryComp<StaminaComponent>(uid, out var stamina))
            return;
        if (!Eligible(uid, poise))
        {
            // Explicit break handling pays the emergency refund. Invalid/removed
            // combat state must never allow an old pool to survive.
            Clear(uid);
            return;
        }

        var now = _timing.CurTime;
        if (now <= combat.RecoveryStarts)
        {
            if (combat.Pending > stamina.StaminaDamage)
                SetPending(uid, combat, stamina.StaminaDamage);
            return;
        }
        var amount = stamina.Critical ? 0f : CombatStaminaMath.RecoveryAmount(
            GetPending(uid), combat.RecoveryPerSecond, combat.LastUpdate, combat.RecoveryStarts, now);
        combat.LastUpdate = now;
        // Set the clock before invoking stamina: movement refresh callbacks may reenter.
        if (amount > 0f)
        {
            var restored = RestoreStamina(uid, amount);
            SetPending(uid, combat, MathF.Min(stamina.StaminaDamage, combat.Pending - restored));
        }
        else if (combat.Pending > stamina.StaminaDamage)
            SetPending(uid, combat, stamina.StaminaDamage);
        else
            Dirty(uid, combat);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CombatStaminaComponent>();
        while (query.MoveNext(out var uid, out var combat))
        {
            if (combat.Pending > 0f && Simulates(uid))
                Advance(uid);
        }
    }
}
