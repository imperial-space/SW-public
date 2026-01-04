using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Wieldable;
using Robust.Shared.Containers;
using Content.Shared.Imperial.Medieval.Skills;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed class BowAutoLoadSystem : EntitySystem
{
    private const string BeltSlotId = "belt";
    private const string BowProjectileSlotId = "projectiles";
    private const string StorageContainerId = "storagebase";

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalWeaponSkillCategoryComponent, ItemWieldedEvent>(OnItemWielded);
    }

    private void OnItemWielded(EntityUid uid, MedievalWeaponSkillCategoryComponent cat, ref ItemWieldedEvent args)
    {
        if (cat.Skill != MedievalWeaponSkillId.Bow)
            return;

        var user = args.User;

        var skillEv = new AttemptBowAutoLoadEvent(uid, user, false);
        RaiseLocalEvent(user, ref skillEv);

        if (!skillEv.Handled)
            return;

        if (!_itemSlots.TryGetSlot(uid, BowProjectileSlotId, out var bowSlot))
            return;

        if (bowSlot.Item != null)
            return;
        if (!_inventory.TryGetSlotEntity(user, BeltSlotId, out var beltEnt) || beltEnt == null)
            return;

        if (!_containers.TryGetContainer(beltEnt.Value, StorageContainerId, out var storage))
            return;

        var itemsToCheck = storage.ContainedEntities.ToList();

        foreach (var item in itemsToCheck)
        {
            if (!_itemSlots.CanInsert(uid, item, user, bowSlot))
                continue;

            if (!_containers.Remove(item, storage))
                continue;

            if (_itemSlots.TryInsert(uid, bowSlot, item, user))
            {
                break;
            }
            else
            {
                _containers.Insert(item, storage);
            }
        }
    }
}
