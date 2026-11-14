using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem
{
    [Dependency]
    private readonly BossHealthBarServerSystem _bossHealthBar = default!;
    private void InitializeHealth()
    {
        SubscribeLocalEvent<BossComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BossComponent, BeforeDamageChangedEvent>(OnBeforeBossDamageChanged);
    }

    internal void InitializeBossHealth(EntityUid uid, BossComponent component)
    {
        EnsureComp<BossActorComponent>(uid);
        EnsureComp<BossArenaAttackSourceComponent>(uid);
        component.MaxHealth = component.Health;
        if (!HasComp<BossEncounterComponent>(uid))
        {
            UpdateHealthBar(uid, component);
            return;
        }
        if (component.UseDamageableHealth && TryComp<DamageableComponent>(uid, out var damageable))
            component.Health = Math.Max(0, component.MaxHealth - damageable.TotalDamage.Float());
        UpdateBossHealth(uid, component);
    }

    private void OnDamageChanged(EntityUid uid, BossComponent component, DamageChangedEvent args)
    {
        if (!HasComp<BossEncounterComponent>(uid))
        {
            if (args.DamageDelta is { } delta && delta.GetTotal() > 0)
                LegacyDamageBoss(uid, delta.GetTotal().Float());
            return;
        }
        if (!component.UseDamageableHealth || component.Finished)               // admin control doesn't bypass this damage path
            return;
        if (args.DamageIncreased)
            LockHealthScaling(uid, component);                                  // first hit locks the health scaling
        component.Health = Math.Max(0, component.MaxHealth - args.Damageable.TotalDamage.Float());
        UpdateBossHealth(uid, component);
    }

    private void OnBeforeBossDamageChanged(EntityUid uid, BossComponent component, ref BeforeDamageChangedEvent args)
    {
        if (component.UseDamageableHealth && component.Finished)
            args.Cancelled = true;
    }

    private void ChangeDamageableBossHealth(EntityUid uid, float damage)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable) || damage == 0)
            return;
        DamageSpecifier change;
        if (damage > 0)
        {
            change = new DamageSpecifier();
            change.DamageDict["Blunt"] = FixedPoint2.New(damage);
        }
        else
        {
            if (damageable.TotalDamage <= 0)
                return;
            change = damageable.Damage * Math.Max(-1f, damage / damageable.TotalDamage.Float());
        }

        _damage.TryChangeDamage(uid, change, ignoreResistances: true, damageable: damageable);
    }

    private void OnHealthBossDefeated(EntityUid uid, BossComponent component)
    {
        if (component.UseDamageableHealth && TryComp<MobStateComponent>(uid, out var mobState))
            _mobState.ChangeMobState(uid, MobState.Dead, mobState);
        UpdateHealthBar(uid, component);
    }

    private void UpdateHealthBar(EntityUid uid, BossComponent component)
    {
        _bossHealthBar.SyncBossHealth(uid, component);
    }
}
