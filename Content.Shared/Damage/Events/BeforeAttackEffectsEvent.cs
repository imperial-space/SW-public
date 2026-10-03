namespace Content.Shared.Damage;

public enum AttackDelivery : byte
{
    Melee,
    Projectile,
    Thrown,
    // A collision effect which may belong to a projectile, a thrown item or a stationary hazard.
    Contact,
}

/// <summary>Raised on a validated target before damage, stamina, contact or status hit effects.</summary>
[ByRefEvent]
public record struct BeforeAttackEffectsEvent(EntityUid Weapon, EntityUid? Attacker, AttackDelivery Delivery, bool Cancelled = false);
