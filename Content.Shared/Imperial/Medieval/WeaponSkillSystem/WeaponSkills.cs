using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Skills;

[Serializable, NetSerializable]
public enum MedievalWeaponSkillId
{
    None = 0,
    OneHandedBlunt,
    OneHandedBluntLight,
    OneHandedSlashSmall,
    OneHandedSlashLarge,
    Spear,
    TwoHanded,
    Crossbow,
    Bow,
    Throwing,
    Shield,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalWeaponSkillCategoryComponent : Component
{
    [DataField("skill", required: true)]
    public MedievalWeaponSkillId Skill = MedievalWeaponSkillId.None;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OneHandedBluntSkillComponent : Component
{
    [DataField("damageMult")] public float DamageMult = 1.15f;
    [DataField("staminaDamage")] public float StaminaDamage = 8f;

    [DataField("bypassType")] public string BypassType = "Blunt";
    [DataField("bypassAmount")] public FixedPoint2 BypassAmount = 2.2f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OneHandedBluntLightSkillComponent : Component
{
    [DataField("damageMult")] public float DamageMult = 1.15f;
    [DataField("staminaDamage")] public float StaminaDamage = 4f;
    [DataField("bypassType")] public string BypassType = "Blunt";
    [DataField("bypassAmount")] public FixedPoint2 BypassAmount = 2.2f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OneHandedSmallSlashSkillComponent : Component
{
    [DataField("damageMult")] public float DamageMult = 1.15f;
    [DataField("bypassType")] public string BypassType = "Piercing";
    [DataField("bypassAmount")] public FixedPoint2 BypassAmount = 1.8f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OneHandedLargeSlashSkillComponent : Component
{
    [DataField("damageMult")] public float DamageMult = 1.25f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SpearSkillComponent : Component
{
    [DataField("bonusType")] public string BonusType = "Piercing";
    [DataField("bonusAmount")] public FixedPoint2 BonusAmount = 7f;
    [DataField("dismountChance")]
    public float DismountChance = 1.0f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class TwoHandedSkillComponent : Component
{
    [DataField("throwOnHitMult")] public float ThrowOnHitMult = 1.5f;
    [DataField("helmetKnockChance")] public float HelmetKnockChance = 0.6f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CrossbowSkillComponent : Component
{
    [DataField("staminaOnHit")] public float StaminaOnHit = 22f;
    [DataField("bypassType")] public string BypassType = "Piercing";
    [DataField("bypassAmount")] public FixedPoint2 BypassAmount = 6f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class BowSkillComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class ThrowingSkillComponent : Component
{
    [DataField("speedMult")] public float SpeedMult = 2.2f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ShieldSkillComponent : Component
{
    [DataField("minBlockFraction")] public float MinBlockFraction = 0.70f;
}


[ByRefEvent]
public record struct GetWeaponSkillLightAttackBonusEvent(
    EntityUid Weapon,
    MedievalWeaponSkillId WeaponSkill,
    float DamageMultiplier,
    DamageSpecifier? BonusDamage,
    DamageSpecifier? BypassDamage,
    float StaminaDamage,
    float HelmetKnockChance,
    bool DismountOnHit
)
{
    public GetWeaponSkillLightAttackBonusEvent(EntityUid weapon, MedievalWeaponSkillId weaponSkill)
        : this(weapon, weaponSkill, 1f, null, null, 0f, 0f, false) { }
}

[ByRefEvent]
public record struct GetWeaponSkillThrowOnHitMultiplierEvent(
    EntityUid Weapon, MedievalWeaponSkillId WeaponSkill, float Multiplier)
{
    public GetWeaponSkillThrowOnHitMultiplierEvent(EntityUid weapon, MedievalWeaponSkillId weaponSkill)
        : this(weapon, weaponSkill, 1f) { }
}

[ByRefEvent]
public record struct GetThrowSpeedMultiplierEvent(EntityUid ThrownItem, EntityUid Thrower, float Multiplier)
{
    public GetThrowSpeedMultiplierEvent(EntityUid thrownItem, EntityUid thrower) : this(thrownItem, thrower, 1f) { }
}

[ByRefEvent]
public record struct GetShieldBlockFractionEvent(EntityUid Shield, EntityUid Wielder, float BlockFraction)
{
    public GetShieldBlockFractionEvent(EntityUid shield, EntityUid wielder) : this(shield, wielder, 0.5f) { }
}

[ByRefEvent]
public record struct GetWeaponSkillProjectileHitBonusEvent(
    EntityUid Weapon, EntityUid Projectile, EntityUid Shooter, MedievalWeaponSkillId WeaponSkill,
    float StaminaDamage, DamageSpecifier? BonusDamage, DamageSpecifier? BypassDamage)
{
    public GetWeaponSkillProjectileHitBonusEvent(EntityUid weapon, EntityUid projectile, EntityUid shooter, MedievalWeaponSkillId weaponSkill)
        : this(weapon, projectile, shooter, weaponSkill, 0f, null, null) { }
}

[ByRefEvent]
public record struct AttemptBowAutoLoadEvent(EntityUid Bow, EntityUid Wielder, bool Handled);
