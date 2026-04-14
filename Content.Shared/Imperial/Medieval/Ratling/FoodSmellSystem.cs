using Content.Shared.Examine;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Content.Shared.Nutrition.Components;

namespace Content.Shared.Ratling;

public sealed class FoodSmellSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoodSmellComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, FoodSmellComponent component, ExaminedEvent args)
    {
        if (!HasComp<CanSmellFoodComponent>(args.Examiner))
            return;
        if (args.Examiner == args.Examined)
            return;
        var hasFood = HasFoodInInventory(args.Examined);
        if (!hasFood)
            return;
        args.PushMarkup(Loc.GetString("food-smelled"));
    }

    private bool HasFoodInInventory(EntityUid target)
    {
        var slots = new[] { "jumpsuit", "outerClothing", "belt", "back", "pocket1", "pocket2" };
        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item) || item == null)
                continue;
            if (HasComp<EdibleComponent>(item.Value))
                return true;
            if (HasFoodInContainer(item.Value))
                return true;
        }
        return false;
    }

    private bool HasFoodInContainer(EntityUid entity)
    {
        if (!HasComp<ContainerManagerComponent>(entity))
            return false;
        var containers = _container.GetAllContainers(entity);
        foreach (var container in containers)
        {
            foreach (var ent in container.ContainedEntities)
            {
                if (!ent.IsValid())
                    continue;
                if (HasComp<EdibleComponent>(ent))
                    return true;

                if (HasFoodInContainer(ent))
                    return true;
            }
        }
        return false;
    }
}
