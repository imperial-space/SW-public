using System;
using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Stacks;
using Content.Shared.Trade;
using Robust.Shared.Containers;
using Robust.Shared.Maths;

namespace Content.Server.Trade;

public sealed partial class TradeTerminalSystem
{
    private void OnInteractUsing(EntityUid uid, TradeTerminalComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        TryInsertOffer(uid, comp, args.User, args.Used);
        args.Handled = true;
    }

    private void OnInsertAttempt(EntityUid uid, TradeTerminalComponent comp, ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        if (_internalTransferTerminals.Contains(uid))
            return;

        if (comp.State != TradeSessionState.Active || comp.HasConfirmed)
            args.Cancel();
    }

    private void OnRemoveAttempt(EntityUid uid, TradeTerminalComponent comp, ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        if (IsRemovalLocked(comp))
            args.Cancel();
    }

    private bool TryInsertOffer(EntityUid uid, TradeTerminalComponent comp, EntityUid user, EntityUid item)
    {
        if (!CanInsertOffer(uid, comp, user) ||
            !TryGetOfferStorage(uid, out var storage))
            return false;

        if (!_storage.TryGetAvailableGridSpace((uid, storage), (item, null), out var location))
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-full", ("max", GetOfferSlotCount(storage))),
                uid,
                user);
            return false;
        }

        return TryInsertOfferAt(uid, comp, user, item, location.Value);
    }

    private bool TryInsertOfferAt(
        EntityUid uid,
        TradeTerminalComponent comp,
        EntityUid user,
        EntityUid item,
        ItemStorageLocation location,
        int amount = 0)
    {
        if (!CanInsertOffer(uid, comp, user) ||
            !TryGetOfferStorage(uid, out var storage))
            return false;

        var slot = location.Position;

        if (!IsValidOfferSlot(storage, slot))
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-insert-failed"), uid, user);
            return false;
        }

        if (TryGetOfferSlotEntity(storage, slot, out var occupiedItem))
        {
            if (TryMergeOfferStacks(uid, comp, user, item, occupiedItem, amount))
                return true;

            _popup.PopupEntity(Loc.GetString("trade-terminal-insert-failed"), uid, user);
            return false;
        }

        if (!_storage.ItemFitsInGridLocation((item, null), (uid, storage), location))
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-insert-failed"), uid, user);
            return false;
        }

        var insertItem = item;
        var spawnedSplit = false;

        if (amount > 0 &&
            TryComp<StackComponent>(item, out var heldStack) &&
            heldStack.Count > amount)
        {
            var split = _stack.Split(item, amount, Transform(uid).Coordinates, heldStack);
            if (split == null)
            {
                _popup.PopupEntity(Loc.GetString("trade-terminal-insert-failed"), uid, user);
                return false;
            }

            insertItem = split.Value;
            spawnedSplit = true;
        }

        if (!_storage.InsertAt((uid, storage), (insertItem, null), location, out _, user, stackAutomatically: false))
        {
            if (spawnedSplit)
                QueueDel(insertItem);

            _popup.PopupEntity(Loc.GetString("trade-terminal-insert-failed"), uid, user);
            return false;
        }
        return true;
    }

    private bool TryMergeOfferStacks(
        EntityUid uid,
        TradeTerminalComponent comp,
        EntityUid user,
        EntityUid item,
        EntityUid target,
        int amount = 0)
    {
        if (!TryComp<StackComponent>(item, out var insertStack) ||
            !TryComp<StackComponent>(target, out var targetStack) ||
            !_stack.TryAdd(
                item,
                target,
                amount > 0 ? Math.Min(amount, insertStack.Count) : insertStack.Count,
                insertStack,
                targetStack))
        {
            return false;
        }

        ResetConfirmations(comp);
        UpdateBothUi(uid, comp);
        DirtyAppearance(uid, comp);
        return true;
    }

    private bool CanInsertOffer(EntityUid uid, TradeTerminalComponent comp, EntityUid user)
    {
        if (!IsOwner(comp, user))
        {
            var popup = comp.Owner == null
                ? "trade-terminal-not-active"
                : "trade-terminal-busy";

            _popup.PopupEntity(Loc.GetString(popup), uid, user);
            return false;
        }

        if (IsInsertLocked(comp))
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-locked"), uid, user);
            return false;
        }

        if (comp.State != TradeSessionState.Active)
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-not-active"), uid, user);
            return false;
        }

        return true;
    }

    private void ResetConfirmations(TradeTerminalComponent comp)
    {
        comp.HasConfirmed = false;

        if (!TryGetPartner(comp, out var partnerId, out var partner) || !partner.HasConfirmed)
            return;

        partner.HasConfirmed = false;

        if (partner.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-offer-changed"),
                partnerId,
                partner.Owner.Value);
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

    private void OnBuiRemoveItem(EntityUid uid, TradeTerminalComponent comp, TradeRemoveItemMessage args)
    {
        if (!IsOwner(comp, args.Actor))
            return;

        if (IsRemovalLocked(comp))
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-locked"), uid, args.Actor);
            return;
        }

        var item = GetEntity(args.Item);
        var container = GetOfferContainer(uid, comp);

        if (!container.Contains(item))
            return;

        if (!_hands.TryPickupAnyHand(args.Actor, item))
        {
            Containers.Remove(item, container, force: true);
            _transform.AttachToGridOrMap(item);
        }
    }

    private void OnBuiInsertHeldItemAt(EntityUid uid, TradeTerminalComponent comp, TradeInsertHeldItemAtMessage args)
    {
        if (!IsOwner(comp, args.Actor))
            return;

        if (!_hands.TryGetActiveItem(args.Actor, out var heldItem))
        {
            _popup.PopupEntity(Loc.GetString("trade-terminal-no-held-item"), uid, args.Actor);
            return;
        }

        TryInsertOfferAt(uid, comp, args.Actor, heldItem.Value, args.Location, args.Amount);
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
}
