using Content.Shared.Hands.Components;

namespace Content.Shared.Inventory;

public partial class InventorySystem
{
    public IEnumerable<EntityUid> GetInventoryEntities(Entity<HandsComponent?, InventoryComponent?> user, SlotFlags flags = SlotFlags.All, bool? check = true)
    {
        if (!Resolve(user.Owner, ref user.Comp2, false))
            yield break;

        var slotEnumerator = new InventorySlotEnumerator(user.Comp2, flags);
        while (slotEnumerator.NextItem(out var item))
            yield return item;
    }
}
