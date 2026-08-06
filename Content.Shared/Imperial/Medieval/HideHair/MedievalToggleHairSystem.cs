using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.ToggleHair.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.ToggleHair.Systems;

public sealed partial class MedievalToggleHairSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedHideableHumanoidLayersSystem _hideableHumanoidLayers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalToggleHairComponent, GotEquippedEvent>(OnMapInit);
        SubscribeLocalEvent<MedievalToggleHairComponent, GotUnequippedEvent>(OnGotUnequipped);

        SubscribeLocalEvent<HideHairToggleEvent>(ToggleEvent);
    }

    /// <summary>
    ///     On map init, either spawn the appropriate entity into the suit slot, or if it already exists, perform some
    ///     sanity checks. Also updates the action icon to show the toggled-entity.
    /// </summary>
    private void OnMapInit(EntityUid uid, MedievalToggleHairComponent comp, GotEquippedEvent ev)
    {
        if (!TryComp(ev.Equipment, out MetaDataComponent? meta) || meta.EntityPrototype == null) return;
        if (HasComp<HideLayerClothingComponent>(ev.Equipment))
            SetLayerVisibility(ev.Equipment!, ev.EquipTarget, hideLayers: true);
        _actions.AddAction(ev.EquipTarget, ref comp.Action, out var action, comp.PrototypeID);
        if (action != null && comp.Action != null)
        {
            action.EntityIcon = ev.Equipment;
            action.Icon = new SpriteSpecifier.EntityPrototype(meta.EntityPrototype.ID);
            Dirty(comp.Action.Value, action);
        }
    }

    private void OnGotUnequipped(EntityUid uid, MedievalToggleHairComponent comp, GotUnequippedEvent ev)
    {
        if (HasComp<HideLayerClothingComponent>(ev.Equipment))
            SetLayerVisibility(ev.Equipment!, ev.EquipTarget, hideLayers: false);
        _actions.RemoveAction(ev.EquipTarget, comp.Action);
    }

    private void ToggleEvent(HideHairToggleEvent ev)
    {
        var uid = ev.Performer;
        if (ev.Handled) return;
        if (!_inventorySystem.TryGetSlotEntity(uid, "head", out var head)) return; // i really wish i could find all prototypes of slots, and not search in Resources/Prototypes/InventoryTemplate
        ev.Handled = true;
        if (HasComp<HideLayerClothingComponent>(head))
        {
            SetLayerVisibility(head.Value!, uid, hideLayers: false);
            RemComp<HideLayerClothingComponent>(head.Value);
            return;
        }
        var comp = EnsureComp<HideLayerClothingComponent>(head.Value);
        comp.Layers.Add(HumanoidVisualLayers.Hair, SlotFlags.HEAD);
        SetLayerVisibility(head.Value!, uid, hideLayers: true);
    }
    public void SetLayerVisibility(
        Entity<HideLayerClothingComponent?, ClothingComponent?> clothing,
        Entity<HideableHumanoidLayersComponent?> user,
        bool hideLayers)
    {
        if (_gameTiming.ApplyingState)
            return;

        if (!Resolve(clothing.Owner, ref clothing.Comp1, ref clothing.Comp2))
            return;

        // logMissing: false, as this clothing might be getting equipped by a non-human.
        if (!Resolve(user.Owner, ref user.Comp, false))
            return;

        hideLayers &= IsEnabled(clothing!);

        var inSlot = clothing.Comp2.InSlotFlag ?? SlotFlags.NONE;

        if (inSlot == SlotFlags.NONE)
            return;

        // iterate the HideLayerClothingComponent's layers map and check that
        // the clothing is (or was)equipped in a matching slot.
        foreach (var (layer, validSlots) in clothing.Comp1.Layers)
        {
            // Only update this layer if we are currently equipped to the relevant slot.
            if (validSlots.HasFlag(inSlot))
                _hideableHumanoidLayers.SetLayerOcclusion(user, layer, hideLayers, inSlot);
        }

        // Fallback for obsolete field: assume we want to hide **all** layers, as long as we are equipped to any
        // relevant clothing slot
#pragma warning disable CS0618 // Type or member is obsolete
        if (clothing.Comp1.Slots is { } slots && clothing.Comp2.Slots.HasFlag(inSlot))
#pragma warning restore CS0618 // Type or member is obsolete
        {
            foreach (var layer in slots)
            {
                _hideableHumanoidLayers.SetLayerOcclusion(user, layer, hideLayers, inSlot);
            }
        }
        // var hideable = user.Comp.HideLayersOnEquip;
        // _humanoid.SetLayerVisibility(user!, layer, !hideLayers, inSlot, ref dirty);
    }

    private bool IsEnabled(Entity<HideLayerClothingComponent, ClothingComponent> clothing)
    {
        // TODO Generalize this
        // I.e., make this and mask component use some generic toggleable.

        if (!clothing.Comp1.HideOnToggle)
            return true;

        if (!TryComp(clothing, out MaskComponent? mask))
            return true;

        return !mask.IsToggled;
    }
}
