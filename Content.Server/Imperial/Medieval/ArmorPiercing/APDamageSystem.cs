using Robust.Shared.Player;
using Content.Shared.Weapons.Melee.Events;
using System.Linq;
using Content.Shared.Damage;
using Content.Server.Damage.Components;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;

namespace Content.Server.Imperial.Medieval.APDamage;

public sealed class APDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<APDamageOnHitComponent, MeleeHitEvent>(DealDamageOnHit);
        SubscribeLocalEvent<APDamageOnHitComponent, ProjectileHitEvent>(DealDamageOnProjectile);
        SubscribeLocalEvent<APDamageOnThrowComponent, ThrowDoHitEvent>(DealDamageOnThrow);
    }

    private void DealDamageOnHit(EntityUid uid, APDamageOnHitComponent component, MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            _damageableSystem.TryChangeDamage(target, component.Damage, true);
        }
    }

    private void DealDamageOnProjectile(EntityUid uid, APDamageOnHitComponent component, ProjectileHitEvent args)
    {
        _damageableSystem.TryChangeDamage(args.Target, component.Damage, true);
    }

    private void DealDamageOnThrow(EntityUid uid, APDamageOnThrowComponent component, ThrowDoHitEvent args)
    {
        _damageableSystem.TryChangeDamage(args.Target, component.Damage, true);
    }
}
