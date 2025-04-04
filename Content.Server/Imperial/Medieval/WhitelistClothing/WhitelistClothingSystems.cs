using Content.Shared.Imperial.WhitelistClothing.Components;
using Content.Shared.Tag;
using Content.Shared.Inventory.Events;
using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared.Inventory;

namespace Content.Server.Imperial.WhitelistClothing.Systems;

public sealed class WhitelistClothingSystems : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WhitelistClothingComponent, DidEquipEvent>(OnEquipAttempt);
    }
    private void OnEquipAttempt(EntityUid uid, WhitelistClothingComponent component, ref DidEquipEvent args)
    {
        if (!_tagSystem.HasTag(args.Equipment, component.Whitelist) && args.Slot.Equals("outerclothing", StringComparison.CurrentCultureIgnoreCase))
            _inventorySystem.TryUnequip(args.Equipee, args.Slot);
    }
}
