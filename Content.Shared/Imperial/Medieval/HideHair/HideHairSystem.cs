using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.HideHair.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.HideHair.Systems;

public sealed partial class HideHairSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HideHairComponent, GotEquippedEvent>(OnMapInit);
        SubscribeLocalEvent<HideHairComponent, GotUnequippedEvent>(OnGotUnequipped);

        SubscribeLocalEvent<HideHairToggleEvent>(ToggleEvent);
    }
    /// <summary>
    ///     On map init, either spawn the appropriate entity into the suit slot, or if it already exists, perform some
    ///     sanity checks. Also updates the action icon to show the toggled-entity.
    /// </summary>
    private void OnMapInit(EntityUid uid, HideHairComponent comp, GotEquippedEvent ev)
    {
        if (HasComp<HideLayerClothingComponent>(ev.Equipment))
            SetLayerVisibility(ev.Equipment!, ev.Equipee, hideLayers: true);
        _actions.AddAction(ev.Equipee, ref comp.Action, comp.PrototypeID);
    }
    private void OnGotUnequipped(EntityUid uid, HideHairComponent comp, GotUnequippedEvent ev)
    {
        if (HasComp<HideLayerClothingComponent>(ev.Equipment))
            SetLayerVisibility(ev.Equipment!, ev.Equipee, hideLayers: false);
        _actions.RemoveAction(ev.Equipee, comp.Action);
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
    Entity<HumanoidAppearanceComponent?> user,
    bool hideLayers) // а вот и паблик, визарды же пожалели сделать публичным метод
    {
        if (_gameTiming.ApplyingState)
            return;

        if (!Resolve(clothing.Owner, ref clothing.Comp1, ref clothing.Comp2))
            return;

        // logMissing: false, as this clothing might be getting equipped by a non-human.
        if (!Resolve(user.Owner, ref user.Comp, false))
            return;

        hideLayers &= IsEnabled(clothing!);

        var hideable = user.Comp.HideLayersOnEquip;
        var inSlot = clothing.Comp2.InSlotFlag ?? SlotFlags.NONE;

        // This method should only be getting called while the clothing is equipped (though possibly currently in
        // the process of getting unequipped).
        DebugTools.AssertNotNull(clothing.Comp2.InSlot);
        DebugTools.AssertNotNull(clothing.Comp2.InSlotFlag);
        DebugTools.AssertNotEqual(inSlot, SlotFlags.NONE);

        var dirty = false;

        // iterate the HideLayerClothingComponent's layers map and check that
        // the clothing is (or was)equipped in a matching slot.
        foreach (var (layer, validSlots) in clothing.Comp1.Layers)
        {
            if (!hideable.Contains(layer))
                continue;

            // Only update this layer if we are currently equipped to the relevant slot.
            if (validSlots.HasFlag(inSlot))
                _humanoid.SetLayerVisibility(user!, layer, !hideLayers, inSlot, ref dirty);
        }

        // Fallback for obsolete field: assume we want to hide **all** layers, as long as we are equipped to any
        // relevant clothing slot
#pragma warning disable CS0618 // Type or member is obsolete
        if (clothing.Comp1.Slots is { } slots && clothing.Comp2.Slots.HasFlag(inSlot))
#pragma warning restore CS0618 // Type or member is obsolete
        {
            foreach (var layer in slots)
            {
                if (hideable.Contains(layer))
                    _humanoid.SetLayerVisibility(user!, layer, !hideLayers, inSlot, ref dirty);
            }
        }

        if (dirty)
            Dirty(user!);
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
