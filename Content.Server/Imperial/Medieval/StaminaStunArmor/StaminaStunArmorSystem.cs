using System;
using Content.Shared.Damage.Events;
using Content.Shared.Imperial.Medieval.Stamina;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.StunArmor;

public sealed class StaminaStunArmorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaminaStunArmorTargetComponent, GetStaminaCritDurationModifiersEvent>(OnGetStaminaCritDurationModifiers);
        SubscribeLocalEvent<StaminaStunArmorComponent, InventoryRelayedEvent<StaminaStunArmorQueryEvent>>(OnArmorQuery);
    }

    private void OnGetStaminaCritDurationModifiers(Entity<StaminaStunArmorTargetComponent> ent, ref GetStaminaCritDurationModifiersEvent args)
    {
        if (!TryComp<InventoryComponent>(ent, out var inventory))
            return;

        var currentTime = _timing.CurTime;

        if (ent.Comp.LastCritTime != TimeSpan.Zero && (currentTime - ent.Comp.LastCritTime) <= ent.Comp.ComboWindow)
        {
            ent.Comp.Combo++;
        }
        else
        {
            ent.Comp.Combo = 0;
        }

        ent.Comp.LastCritTime = currentTime;

        var queryEvent = new StaminaStunArmorQueryEvent();
        _inventory.RelayEvent((ent.Owner, inventory), ref queryEvent);

        var armorEfficiency = Math.Max(0f, 1f - (ent.Comp.Combo * ent.Comp.EfficiencyDropPerCombo));

        var effectiveFlatReduction = queryEvent.FlatReduction * armorEfficiency;

        var effectiveMultiplier = 1f - ((1f - queryEvent.Multiplier) * armorEfficiency);

        args.Modifier = MathF.Max(0f, (args.Modifier - effectiveFlatReduction) * effectiveMultiplier);
    }

    private void OnArmorQuery(Entity<StaminaStunArmorComponent> ent, ref InventoryRelayedEvent<StaminaStunArmorQueryEvent> args)
    {
        args.Args.Multiplier *= ent.Comp.Multiplier;
        args.Args.FlatReduction += ent.Comp.FlatReduction;
    }
}
