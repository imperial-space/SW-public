using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

public partial class SharedSmithingSystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    protected void InitializeFurnaceSystem()
    {
        SubscribeLocalEvent<SmithingFurnaceComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<SmithingFurnaceComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<SmithingFurnaceComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SmithingFurnaceComponent> ent, ref MapInitEvent args)
    {
        _itemSlots.AddItemSlot(ent, "furnace-slot", new ItemSlot());
    }

    private void OnEntInserted(Entity<SmithingFurnaceComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<SmithingWorkpieceComponent>(args.Entity, out var workpieceComponent))
        {
            return;
        }
    }

    private void OnEntRemoved(Entity<SmithingFurnaceComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!TryComp<SmithingWorkpieceComponent>(args.Entity, out var workpieceComponent))
        {
            //TODO: Do bad stuff
            return;
        }

        workpieceComponent.ReadyToForge = true;
    }
}
