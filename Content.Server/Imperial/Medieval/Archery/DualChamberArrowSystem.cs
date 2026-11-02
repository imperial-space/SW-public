using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Imperial.Medieval.Archery;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Interaction;


namespace Content.Server.Imperial.Medieval.Archery;

public sealed class DualChamberArrowSystem : EntitySystem
{
    [Dependency]
    private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency]
    private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency]
    private readonly PuddleSystem _puddle = default!;
    [Dependency]
    private readonly SharedAudioSystem _audio = default!;
    [Dependency]
    private readonly SharedPopupSystem _popup = default!;
    [Dependency]
    private readonly SolutionTransferSystem _solutionTransfer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DualChamberArrowComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DualChamberArrowComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<DualChamberArrowComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DualChamberArrowComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DualChamberArrowComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private string GetActiveChamberSlot(DualChamberArrowComponent component)
    {
        return component.ActiveChamber == 2
            ? component.ChamberTwoSlot
            : component.ChamberOneSlot;
    }

    private void ToggleChamber(DualChamberArrowComponent component)
    {
        component.ActiveChamber = component.ActiveChamber == 1 ? 2 : 1;
    }

    private void OnExamined(EntityUid uid, DualChamberArrowComponent component, ExaminedEvent args)
    {
        var chamberOneVolume = FixedPoint2.Zero;
        var chamberOneMax = FixedPoint2.New(5);

        var chamberTwoVolume = FixedPoint2.Zero;
        var chamberTwoMax = FixedPoint2.New(5);

        if (_itemSlots.GetItemOrNull(uid, component.ChamberOneSlot) is { } chamberOneVial && _solutionContainer.TryGetSolution(chamberOneVial, component.SolutionName, out _, out var chamberOneSolution))
        {
            chamberOneVolume = chamberOneSolution.Volume;
            chamberOneMax = chamberOneSolution.MaxVolume;
        }

        if (_itemSlots.GetItemOrNull(uid, component.ChamberTwoSlot) is { } chamberTwoVial && _solutionContainer.TryGetSolution(chamberTwoVial, component.SolutionName, out _, out var chamberTwoSolution))
        {
            chamberTwoVolume = chamberTwoSolution.Volume;
            chamberTwoMax = chamberTwoSolution.MaxVolume;
        }

        args.PushText(
            Loc.GetString(
                "dual-chamber-arrow-examine",
                ("active", component.ActiveChamber),
                ("one", chamberOneVolume),
                ("oneMax", chamberOneMax),
                ("two", chamberTwoVolume),
                ("twoMax", chamberTwoMax)));
    }

    private void OnInteractUsing(EntityUid uid, DualChamberArrowComponent component, InteractUsingEvent args)
    {
        if (args.Handled || component.Spent)
            return;

        if (!TryComp<SolutionTransferComponent>(args.Used, out var transfer) ||
            !transfer.CanSend)
        {
            return;
        }

        if (!_solutionContainer.TryGetDrainableSolution(args.Used, out var sourceSolution, out _))
        {
            return;
        }

        var filledChamber = component.ActiveChamber;
        var slot = GetActiveChamberSlot(component);

        if (_itemSlots.GetItemOrNull(uid, slot) is not { } vial)
            return;

        if (!_solutionContainer.TryGetSolution(vial, component.SolutionName, out var targetSolution, out var target))
        {
            return;
        }

        // Already full.
        if (target.Volume >= target.MaxVolume)
            return;

        var oldVolume = target.Volume;

        args.Handled = true;

        _solutionTransfer.Transfer(args.User,
        args.Used,
        sourceSolution.Value,
        vial,
        targetSolution.Value,
        transfer.TransferAmount);

        // Get the actual chamber state after the transfer.
        if (!_solutionContainer.TryGetSolution(vial, component.SolutionName, out _, out var updated))
        {
            return;
        }

        // Nothing was actually transferred.
        if (updated.Volume <= oldVolume)
            return;

        // Do nothing special until the chamber reaches full capacity.
        if (updated.Volume < updated.MaxVolume)
            return;

        var otherChamber = filledChamber == 1 ? 2 : 1;

        var otherSlot = otherChamber == 1
            ? component.ChamberOneSlot
            : component.ChamberTwoSlot;

        if (_itemSlots.GetItemOrNull(uid, otherSlot) is not { } otherVial || !_solutionContainer.TryGetSolution(otherVial, component.SolutionName, out _, out var otherSolution))
        {
            return;
        }

        // Other chamber is already full.
        if (otherSolution.Volume >= otherSolution.MaxVolume)
        {
            _popup.PopupEntity(Loc.GetString("dual-chamber-arrow-filled-both", ("filled", filledChamber)), args.User, args.User);

            return;
        }

        // Explicitly select the other chamber.
        component.ActiveChamber = otherChamber;

        _popup.PopupEntity(Loc.GetString("dual-chamber-arrow-filled-switched", ("filled", filledChamber), ("active", component.ActiveChamber)), args.User, args.User);
    }

    private void OnGetVerbs(EntityUid uid, DualChamberArrowComponent component, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || component.Spent)
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dual-chamber-arrow-toggle-chamber"),

            Act = () =>
            {
                ToggleChamber(component);

                _popup.PopupEntity(Loc.GetString("dual-chamber-arrow-selected", ("chamber", component.ActiveChamber)), uid, user);
            }
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dual-chamber-arrow-spill-chamber", ("chamber", component.ActiveChamber)),

            Act = () =>
            {
                SpillActiveChamber(uid, component, user);
            }
        });
    }

    private void SpillActiveChamber(EntityUid uid, DualChamberArrowComponent component, EntityUid user)
    {
        var chamber = component.ActiveChamber;
        var slot = GetActiveChamberSlot(component);

        if (_itemSlots.GetItemOrNull(uid, slot) is not { } vial)
            return;

        if (!_solutionContainer.TryGetSolution(vial, component.SolutionName, out var solutionEntity, out var solution))
        {
            return;
        }

        if (solution.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dual-chamber-arrow-spill-empty", ("chamber", chamber)), uid, user);
            return;
        }

        var spilled = _solutionContainer.SplitSolution(solutionEntity.Value, solution.Volume);

        if (spilled.Volume <= FixedPoint2.Zero)
            return;

        if (!_puddle.TrySpillAt(uid, spilled, out _, sound: true))
        {
            _solutionContainer.TryAddSolution(solutionEntity.Value, spilled);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dual-chamber-arrow-spilled", ("chamber", chamber)), uid, user);
    }

    private void OnAfterInteract(Entity<DualChamberArrowComponent> arrow, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || arrow.Comp.Spent || args.Target is not { } target)
        {
            return;
        }

        // The target must be a container capable of receiving solution.
        if (!TryComp<SolutionTransferComponent>(target, out var transfer) || !transfer.CanReceive)
        {
            return;
        }

        var slot = GetActiveChamberSlot(arrow.Comp);

        if (_itemSlots.GetItemOrNull(arrow.Owner, slot) is not { } vial)
            return;

        // Get the selected chamber's solution.
        if (!_solutionContainer.TryGetSolution(vial, arrow.Comp.SolutionName, out var sourceSolution, out var source))
        {
            return;
        }

        if (source.Volume <= FixedPoint2.Zero)
            return;

        // Find the destination container's refillable solution.
        if (!_solutionContainer.TryGetRefillableSolution(target, out var targetSolution, out var destination))
        {
            return;
        }

        // Destination is already full.
        if (destination.Volume >= destination.MaxVolume)
            return;

        var oldVolume = destination.Volume;

        args.Handled = true;

        _solutionTransfer.Transfer(args.User, vial, sourceSolution.Value, target, targetSolution.Value, source.Volume);

        // Re-read the destination so we know how much actually transferred.
        if (!_solutionContainer.TryGetRefillableSolution(target, out _, out var updatedDestination))
        {
            return;
        }

        var transferred = updatedDestination.Volume - oldVolume;

        if (transferred <= FixedPoint2.Zero)
            return;

        _popup.PopupEntity(Loc.GetString("dual-chamber-arrow-transfer", ("amount", transferred)), args.User, args.User);
    }

    private void OnProjectileHit(EntityUid uid, DualChamberArrowComponent component, ref ProjectileHitEvent args)
    {
        ActivateArrow(uid, component);
    }

    private void ActivateArrow(EntityUid uid, DualChamberArrowComponent component)
    {
        if (component.Spent)
            return;
        // Prevent multiple impact events from activating the arrow twice.
        component.Spent = true;
        // Get the hidden 10u reaction solution.
        if (!_solutionContainer.TryGetSolution(uid, component.ReactionSolutionName, out var reactionEntity, out _))
        {
            BreakArrow(uid, component);
            return;
        }
        // Empty chamber 1.
        if (_itemSlots.GetItemOrNull(uid, component.ChamberOneSlot) is { } chamberOneVial && _solutionContainer.TryGetSolution(chamberOneVial, component.SolutionName, out var chamberOneEntity, out var chamberOneSolution) && chamberOneSolution.Volume > FixedPoint2.Zero)
        {
            var contents = _solutionContainer.SplitSolution(chamberOneEntity.Value, chamberOneSolution.Volume);
            _solutionContainer.TryAddSolution(reactionEntity.Value, contents);
        }
        // Empty chamber 2.
        if (_itemSlots.GetItemOrNull(uid, component.ChamberTwoSlot) is { } chamberTwoVial && _solutionContainer.TryGetSolution(chamberTwoVial, component.SolutionName, out var chamberTwoEntity, out var chamberTwoSolution) && chamberTwoSolution.Volume > FixedPoint2.Zero)
        {
            var contents = _solutionContainer.SplitSolution(chamberTwoEntity.Value, chamberTwoSolution.Volume);
            _solutionContainer.TryAddSolution(reactionEntity.Value, contents);
        }

        if (_solutionContainer.TryGetSolution(uid, component.ReactionSolutionName, out var finalReactionEntity, out var finalReaction) && finalReaction.Volume > FixedPoint2.Zero)
        {
            var remainder = _solutionContainer.SplitSolution(finalReactionEntity.Value, finalReaction.Volume);

            if (remainder.Volume > FixedPoint2.Zero)
            {
                _puddle.TrySpillAt(uid, remainder, out _, sound: false);
            }
        }

        BreakArrow(uid, component);
    }

    private void BreakArrow(EntityUid uid, DualChamberArrowComponent component)
    {
        _audio.PlayPvs(component.BreakSound, Transform(uid).Coordinates);

        if (_itemSlots.GetItemOrNull(uid, component.ChamberOneSlot) is { } chamberOneVial)
            QueueDel(chamberOneVial);

        if (_itemSlots.GetItemOrNull(uid, component.ChamberTwoSlot) is { } chamberTwoVial)
            QueueDel(chamberTwoVial);

        QueueDel(uid);
    }
}