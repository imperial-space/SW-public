namespace Content.Shared.Damage.Events;

/// <summary>Adjusts a melee or thrown strike before damage is applied.</summary>
[ByRefEvent]
public record struct PhysicalStrikeEvent(EntityUid Target, EntityUid Weapon, DamageSpecifier Damage, bool Thrown = false, bool Empowered = false);

/// <summary>Reports a strike which dealt damage, allowing its effects to commit their cooldowns.</summary>
[ByRefEvent]
public readonly record struct PhysicalStrikeLandedEvent(EntityUid Target, bool Empowered);
