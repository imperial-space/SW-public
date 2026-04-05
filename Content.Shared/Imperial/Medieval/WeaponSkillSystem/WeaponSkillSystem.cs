using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.Skills;

public sealed class WeaponSkillSystem : EntitySystem
{
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OneHandedBluntSkillComponent, GetWeaponSkillLightAttackBonusEvent>(OnLightBonus_OneHandedBlunt);
        SubscribeLocalEvent<OneHandedBluntLightSkillComponent, GetWeaponSkillLightAttackBonusEvent>(OnLightBonus_OneHandedBluntLight);
        SubscribeLocalEvent<OneHandedSmallSlashSkillComponent, GetWeaponSkillLightAttackBonusEvent>(OnLightBonus_OneHandedSmallSlash);
        SubscribeLocalEvent<OneHandedLargeSlashSkillComponent, GetWeaponSkillLightAttackBonusEvent>(OnLightBonus_OneHandedLargeSlash);
        SubscribeLocalEvent<SpearSkillComponent, GetWeaponSkillLightAttackBonusEvent>(OnLightBonus_Spear);
        SubscribeLocalEvent<TwoHandedSkillComponent, GetWeaponSkillLightAttackBonusEvent>(OnLightBonus_TwoHanded);

        SubscribeLocalEvent<TwoHandedSkillComponent, GetWeaponSkillThrowOnHitMultiplierEvent>(OnThrowOnHit_TwoHanded);
        SubscribeLocalEvent<ThrowingSkillComponent, GetThrowSpeedMultiplierEvent>(OnThrowSpeed_Throwing);
        SubscribeLocalEvent<ShieldSkillComponent, GetShieldBlockFractionEvent>(OnShieldBlock_Shield);

        SubscribeLocalEvent<CrossbowSkillComponent, GetWeaponSkillProjectileHitBonusEvent>(OnProjectileHit_Crossbow);
        SubscribeLocalEvent<BowSkillComponent, AttemptBowAutoLoadEvent>(OnBowAutoLoad_Bow);
    }

    private static bool WeaponMatches(MedievalWeaponSkillId current, MedievalWeaponSkillId expected)
        => current == expected;

    #region Light Attack Bonuses

    private void OnLightBonus_OneHandedBlunt(EntityUid uid, OneHandedBluntSkillComponent comp, ref GetWeaponSkillLightAttackBonusEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.OneHandedBlunt)) return;

        ev = ev with
        {
            DamageMultiplier = ev.DamageMultiplier * comp.DamageMult,
            StaminaDamage = ev.StaminaDamage + comp.StaminaDamage,
            BypassDamage = AddDamage(ev.BypassDamage, comp.BypassType, comp.BypassAmount),
        };
    }

    private void OnLightBonus_OneHandedBluntLight(EntityUid uid, OneHandedBluntLightSkillComponent comp, ref GetWeaponSkillLightAttackBonusEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.OneHandedBluntLight)) return;

        ev = ev with
        {
            DamageMultiplier = ev.DamageMultiplier * comp.DamageMult,
            StaminaDamage = ev.StaminaDamage + comp.StaminaDamage,
            BypassDamage = AddDamage(ev.BypassDamage, comp.BypassType, comp.BypassAmount),
        };
    }

    private void OnLightBonus_OneHandedSmallSlash(EntityUid uid, OneHandedSmallSlashSkillComponent comp, ref GetWeaponSkillLightAttackBonusEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.OneHandedSlashSmall)) return;

        ev = ev with
        {
            DamageMultiplier = ev.DamageMultiplier * comp.DamageMult,
            BypassDamage = AddDamage(ev.BypassDamage, comp.BypassType, comp.BypassAmount),
        };
    }

    private void OnLightBonus_OneHandedLargeSlash(EntityUid uid, OneHandedLargeSlashSkillComponent comp, ref GetWeaponSkillLightAttackBonusEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.OneHandedSlashLarge)) return;

        ev = ev with { DamageMultiplier = ev.DamageMultiplier * comp.DamageMult };
    }

    private void OnLightBonus_Spear(EntityUid uid, SpearSkillComponent comp, ref GetWeaponSkillLightAttackBonusEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.Spear)) return;

        ev = ev with
        {
            BonusDamage = AddDamage(ev.BonusDamage, comp.BonusType, comp.BonusAmount),
            DismountOnHit = _random.Prob(comp.DismountChance)
        };
    }

    private void OnLightBonus_TwoHanded(EntityUid uid, TwoHandedSkillComponent comp, ref GetWeaponSkillLightAttackBonusEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.TwoHanded)) return;

        ev = ev with
        {
            HelmetKnockChance = MathF.Max(ev.HelmetKnockChance, comp.HelmetKnockChance),
        };
    }

    #endregion

    #region Special Effects & Projectiles

    private void OnThrowOnHit_TwoHanded(EntityUid uid, TwoHandedSkillComponent comp, ref GetWeaponSkillThrowOnHitMultiplierEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.TwoHanded)) return;

        ev = ev with { Multiplier = ev.Multiplier * comp.ThrowOnHitMult };
    }

    private void OnThrowSpeed_Throwing(EntityUid uid, ThrowingSkillComponent comp, ref GetThrowSpeedMultiplierEvent ev)
    {
        ev = ev with { Multiplier = ev.Multiplier * comp.SpeedMult };
    }

    private void OnShieldBlock_Shield(EntityUid uid, ShieldSkillComponent comp, ref GetShieldBlockFractionEvent ev)
    {
        ev = ev with { BlockFraction = MathF.Max(ev.BlockFraction, comp.MinBlockFraction) };
    }

    private void OnProjectileHit_Crossbow(EntityUid uid, CrossbowSkillComponent comp, ref GetWeaponSkillProjectileHitBonusEvent ev)
    {
        if (!WeaponMatches(ev.WeaponSkill, MedievalWeaponSkillId.Crossbow)) return;

        ev = ev with
        {
            StaminaDamage = ev.StaminaDamage + comp.StaminaOnHit,
            BypassDamage = AddDamage(ev.BypassDamage, comp.BypassType, comp.BypassAmount),
        };
    }

    private void OnBowAutoLoad_Bow(EntityUid uid, BowSkillComponent comp, ref AttemptBowAutoLoadEvent ev)
    {
        ev = ev with { Handled = true };
    }

    #endregion

    #region Helpers

    private static DamageSpecifier AddDamage(DamageSpecifier? existing, string type, FixedPoint2 amount)
    {
        if (amount == 0) return existing ?? new DamageSpecifier();

        var spec = existing == null ? new DamageSpecifier() : new DamageSpecifier(existing);

        if (!spec.DamageDict.ContainsKey(type))
        {
            spec.DamageDict[type] = amount;
        }
        else
        {
            spec.DamageDict[type] += amount;
        }

        return spec;
    }

    #endregion
}
