using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Archery;
using Content.Shared.Projectiles;
using Content.Shared.Inventory;
using Content.Server.Fluids.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.Imperial.Medieval.Archery;

public sealed class PayloadArrowSystem : EntitySystem
{
    [Dependency]
    private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency]
    private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency]
    private readonly ReactiveSystem _reactive = default!;
    [Dependency]
    private readonly InventorySystem _inventory = default!;
    [Dependency]
    private readonly PuddleSystem _puddle = default!;
    [Dependency]
    private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency]
    private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PayloadArrowComponent, ProjectileHitEvent>(OnProjectileHit);
        //SubscribeLocalEvent<PayloadArrowComponent, ContainerIsInsertingAttemptEvent>(OnPayloadInsertAttempt);
        SubscribeLocalEvent<ArrowInjectionResistanceComponent, InventoryRelayedEvent<GetArrowInjectionResistanceEvent>>(OnInjectionResistance);
    }

    private void OnPayloadInsertAttempt(EntityUid arrow, PayloadArrowComponent component, ContainerIsInsertingAttemptEvent args)
    {
        // Only validate the payload vial slot.
        if (args.Container.ID != component.PayloadSlot)
            return;

        var vial = args.EntityUid;

        if (!_solutionContainer.TryGetSolution(vial, component.SolutionName, out _, out var solution))
        {
            args.Cancel();
            return;
        }

        // Empty compatible vials are allowed.
        if (solution.Volume <= FixedPoint2.Zero)
            return;

        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (quantity <= FixedPoint2.Zero)
                continue;

            // Explicitly allowed reagent.
            if (component.AllowedReagents.Contains(reagent.Prototype))
                continue;

            var prototype = _prototypeManager.Index<ReagentPrototype>(reagent.Prototype);

            // Automatically allowed harmful group.
            if (component.AllowedReagentGroups.Contains(prototype.Group))
                continue;

            // At least one forbidden reagent means the vial cannot be inserted.
            args.Cancel();
            return;
        }
    }

    private void OnInjectionResistance(EntityUid uid, ArrowInjectionResistanceComponent component, InventoryRelayedEvent<GetArrowInjectionResistanceEvent> args)
    {
        args.Args.Resistance += component.Resistance;
    }

    private void OnProjectileHit(
        EntityUid uid,
        PayloadArrowComponent component,
        ref ProjectileHitEvent args)
    {
        if (component.Spent)
            return;

        if (!TryComp<BloodstreamComponent>(args.Target, out var bloodstream))
            return;

        if (_itemSlots.GetItemOrNull(uid, component.PayloadSlot) is not { } vial)
            return;

        if (!_solutionContainer.TryGetSolution(
                vial,
                component.SolutionName,
                out var solutionEntity,
                out var solution))
        {
            return;
        }

        if (solution.Volume <= FixedPoint2.Zero)
            return;

        var resistanceEvent = new GetArrowInjectionResistanceEvent();

        if (TryComp<InventoryComponent>(args.Target, out var inventory))
        {
            _inventory.RelayEvent((args.Target, inventory), resistanceEvent);
        }

        var injectionResistance = Math.Clamp(resistanceEvent.Resistance, 0f, 0.4f);

        // Remove the entire payload from the vial.
        var payload = _solutionContainer.SplitSolution(solutionEntity.Value, solution.Volume);

        if (payload.Volume <= FixedPoint2.Zero)
            return;

        // Split the payload according to armor resistance.
        //
        // SplitSolution removes the requested amount from "payload"
        // and returns it as a new solution.
        //
        // After this:
        // payload = amount that gets injected
        // spilled = amount stopped by armor
        var resistedAmount = payload.Volume * injectionResistance;

        var spilled = payload.SplitSolution(resistedAmount);

        // Inject the portion that penetrated the armor.
        if (!_bloodstream.TryAddToChemicals((args.Target, bloodstream), payload))
        {
            // Something prevented injection.
            // Restore both portions to the vial.
            _solutionContainer.TryAddSolution(solutionEntity.Value, payload);

            _solutionContainer.TryAddSolution(solutionEntity.Value, spilled);

            return;
        }

        // Trigger injection-specific reagent effects.
        _reactive.DoEntityReaction(args.Target, payload, ReactionMethod.Injection);

        // Anything stopped by armor gets spilled onto the ground.
        if (spilled.Volume > FixedPoint2.Zero)
        {
            // Normally spill at the target.
            // If that fails, try the arrow's position as a fallback.
            if (!_puddle.TrySpillAt(args.Target, spilled, out _, sound: false))
            {
                _puddle.TrySpillAt(uid, spilled, out _, sound: false);
            }
        }

        // The payload has now been used.
        component.Spent = true;

        // The vial is lost when the payload arrow activates.
        QueueDel(vial);
    }
}