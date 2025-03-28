using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

public partial class SharedSmithingSystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    protected virtual void InitializeFurnaceSystem()
    {
        SubscribeLocalEvent<SmithingFurnaceComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SmithingFurnaceComponent> ent, ref MapInitEvent args)
    {
        _itemSlots.AddItemSlot(ent, "furnace-slot", ent.Comp.MeltingSlot);
    }
}
