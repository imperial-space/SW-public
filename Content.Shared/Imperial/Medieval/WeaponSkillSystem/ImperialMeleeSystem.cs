using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.WeaponSkillSystem;

public sealed class ImperialMeleeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;

    public readonly record struct ImperialSkillEffects(
        float DamageMultiplier,
        DamageSpecifier? BonusDamage,
        DamageSpecifier? BypassDamage,
        float StaminaDamage,
        float HelmetKnockChance,
        bool DismountOnHit,
        bool IsActive
    );

    public ImperialSkillEffects GetSkillEffects(EntityUid weapon, EntityUid user)
    {
        if (!TryComp<MedievalWeaponSkillCategoryComponent>(weapon, out var skillCat))
            return new ImperialSkillEffects(1f, null, null, 0f, 0f, false, false);

        var skillEv = new GetWeaponSkillLightAttackBonusEvent(weapon, skillCat.Skill);
        RaiseLocalEvent(user, ref skillEv);

        var mult = skillEv.DamageMultiplier;
        if (float.IsNaN(mult) || float.IsInfinity(mult)) mult = 1f;

        return new ImperialSkillEffects(
            MathF.Max(0f, mult),
            skillEv.BonusDamage,
            skillEv.BypassDamage,
            skillEv.StaminaDamage,
            skillEv.HelmetKnockChance,
            skillEv.DismountOnHit,
            true
        );
    }

    public DamageSpecifier? ApplySkillEffects(EntityUid target, EntityUid user, EntityUid weapon,
        ImperialSkillEffects effects, DamageSpecifier? mainDamageResult, MeleeWeaponComponent component)
    {
        if (!effects.IsActive) return null;

        DamageSpecifier? bypassResult = null;
        if (effects.BypassDamage != null && effects.BypassDamage.GetTotal() > FixedPoint2.Zero)
        {
            bypassResult = _damageable.TryChangeDamage(target, effects.BypassDamage, origin: user, ignoreResistances: true);
        }

        bool anyDamage = (mainDamageResult != null && !mainDamageResult.Empty) ||
                         (bypassResult != null && !bypassResult.Empty);

        if (anyDamage)
        {
            if (effects.StaminaDamage > 0f)
            {
                _stamina.TakeStaminaDamage(target, effects.StaminaDamage, visual: false, source: user, with: weapon == user ? null : weapon);
            }

            if (effects.HelmetKnockChance > 0f && _random.Prob(effects.HelmetKnockChance))
            {
                _inventory.TryUnequip(target, "head", force: true, silent: true);
            }

            if (effects.DismountOnHit)
            {
                if (TryComp<StrapComponent>(target, out var strap))
                {
                    foreach (var rider in new List<EntityUid>(strap.BuckledEntities))
                    {
                        _buckle.TryUnbuckle(rider, rider, true);
                    }
                }
            }
        }

        return bypassResult;
    }
}
