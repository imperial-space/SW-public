namespace Content.Shared.Wieldable;

/// <summary>Adjusts the free hands needed to wield an item and whether wielding grants its damage bonus.</summary>
[ByRefEvent]
public record struct GetWieldHandRequirementEvent(EntityUid Item, int Hands, bool SuppressDamageBonus = false);
