using Content.Server.Stack;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed class TradingItemDeliverySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public void Deliver(EntityUid item, EntityUid recipient)
    {
        if (!IsRemaining(item) || !TryComp<TransformComponent>(recipient, out var recipientTransform))
            return;

        _transform.SetCoordinates(item, recipientTransform.Coordinates);

        if (!HasComp<ItemComponent>(item))
            return;

        if (HasComp<MedievalCurrencyComponent>(item))
        {
            foreach (var candidate in GetInventoryItems(recipient))
            {
                if (!HasComp<MedievalCoinBagComponent>(candidate) ||
                    !TryComp<StorageComponent>(candidate, out var storage))
                {
                    continue;
                }

                _storage.Insert(
                    candidate,
                    item,
                    out _,
                    recipient,
                    storage,
                    stackAutomatically: !HasComp<TradingLotBlockedComponent>(item));
                if (IsDeliveredTo(item, candidate))
                    return;
            }
        }

        MergeIntoHeldStacks(item, recipient);
        if (!IsRemaining(item) || _hands.TryPickupAnyHand(recipient, item))
            return;

        if (TryInsertIntoEquippedStorage(item, recipient, SlotFlags.BACK))
            return;

        if (TryInsertIntoEquippedStorage(item, recipient, SlotFlags.BELT))
            return;

        TryInsertIntoPockets(item, recipient);
    }

    private bool TryInsertIntoEquippedStorage(EntityUid item, EntityUid recipient, SlotFlags flags)
    {
        var slots = _inventory.GetSlotEnumerator(recipient, flags);
        while (slots.NextItem(out var equipped))
        {
            if (!TryComp<StorageComponent>(equipped, out var storage))
                continue;

            _storage.Insert(
                equipped,
                item,
                out _,
                recipient,
                storage,
                stackAutomatically: !HasComp<TradingLotBlockedComponent>(item));
            if (IsDeliveredTo(item, equipped))
                return true;
        }

        return !IsRemaining(item);
    }

    private void TryInsertIntoPockets(EntityUid item, EntityUid recipient)
    {
        var occupiedPockets = _inventory.GetSlotEnumerator(recipient, SlotFlags.POCKET);
        while (occupiedPockets.NextItem(out var pocketItem))
        {
            if (TryMergeStacks(item, pocketItem) && !IsRemaining(item))
                return;
        }

        if (!_inventory.TryGetSlots(recipient, out var slots))
            return;

        foreach (var slot in slots)
        {
            if ((slot.SlotFlags & SlotFlags.POCKET) == 0 ||
                _inventory.TryGetSlotEntity(recipient, slot.Name, out _))
            {
                continue;
            }

            if (_inventory.TryEquip(recipient, item, slot.Name, silent: true))
                return;
        }
    }

    private void MergeIntoHeldStacks(EntityUid item, EntityUid recipient)
    {
        foreach (var held in _hands.EnumerateHeld(recipient))
        {
            if (TryMergeStacks(item, held) && !IsRemaining(item))
                return;
        }
    }

    private bool TryMergeStacks(EntityUid item, EntityUid target)
    {
        var blocksTradingLot = HasComp<TradingLotBlockedComponent>(item);
        if (!TryComp<StackComponent>(item, out var itemStack) ||
            !TryComp<StackComponent>(target, out var targetStack) ||
            !_stack.TryAdd(item, target, itemStack, targetStack))
        {
            return false;
        }

        if (blocksTradingLot)
            EnsureComp<TradingLotBlockedComponent>(target);

        return true;
    }

    private bool IsDeliveredTo(EntityUid item, EntityUid containerOwner)
    {
        if (!IsRemaining(item))
            return true;

        return _containers.TryGetContainingContainer(item, out var container) &&
               container.Owner == containerOwner;
    }

    private bool IsRemaining(EntityUid item)
    {
        if (!Exists(item) || TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item))
            return false;

        return !TryComp<StackComponent>(item, out var stack) || stack.Count > 0;
    }

    private List<EntityUid> GetInventoryItems(EntityUid user)
    {
        var items = new List<EntityUid>();
        var pending = new Queue<EntityUid>(_inventory.GetHandOrInventoryEntities(user));
        var visited = new HashSet<EntityUid>();
        while (pending.TryDequeue(out var candidate))
        {
            if (!visited.Add(candidate))
                continue;

            items.Add(candidate);
            if (!TryComp<ContainerManagerComponent>(candidate, out var containerManager))
                continue;

            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    pending.Enqueue(contained);
                }
            }
        }

        return items;
    }
}
