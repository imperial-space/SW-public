using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Projectiles;

namespace Content.Shared.Imperial.Medieval.WeaponSkillSystem;

public sealed class ImperialProjectileSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(EntityUid uid, ProjectileComponent component, ref ProjectileHitEvent args)
    {
        if (component.Weapon == null || component.Shooter == null || args.Target == EntityUid.Invalid)
            return;

        var weapon = component.Weapon.Value;
        var user = component.Shooter.Value;
        var target = args.Target;

        if (!TryComp<MedievalWeaponSkillCategoryComponent>(weapon, out var skillCat))
            return;

        var ev = new GetWeaponSkillProjectileHitBonusEvent(weapon, uid, user, skillCat.Skill);
        RaiseLocalEvent(user, ref ev);

        if (ev.StaminaDamage > 0f || (ev.BypassDamage != null && ev.BypassDamage.GetTotal() > FixedPoint2.Zero))
        {
            if (ev.StaminaDamage > 0f)
            {
                _stamina.TakeStaminaDamage(target, ev.StaminaDamage, visual: false, source: user, with: weapon);
            }

            if (ev.BypassDamage != null && ev.BypassDamage.GetTotal() > FixedPoint2.Zero)
            {
                _damageable.TryChangeDamage(target, ev.BypassDamage, origin: user, ignoreResistances: true);
            }
        }
    }
}
