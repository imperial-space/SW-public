namespace Content.Shared.Inventory.Events;

/// <summary>Allows an item to fit an equipment slot beyond its normal clothing or pocket rules.</summary>
[ByRefEvent]
public record struct AdditionalSlotFitEvent(EntityUid Item, SlotFlags Slot, bool Allowed = false);
