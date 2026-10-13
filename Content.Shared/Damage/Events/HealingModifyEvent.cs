namespace Content.Shared.Damage;

/// <summary>Patient-side healing modifiers, independent of damage resistance bypass.</summary>
[ByRefEvent]
public record struct HealingModifyEvent(DamageSpecifier Damage);
