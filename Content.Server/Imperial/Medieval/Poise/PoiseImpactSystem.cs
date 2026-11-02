using Content.Server.Spawners.EntitySystems;
using Content.Server.Imperial.Medieval.Boss;
using Content.Server.Imperial.Medieval.APDamage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Archery;
using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;
using Content.Server.Weapons.Ranged.Systems;

namespace Content.Server.Imperial.Medieval.Poise;

/// <summary>
/// Applies configured ranged and contact poise once per attack and target.
/// Melee attacks continue to use the ordinary PoiseDamage/AttackedEvent path.
/// </summary>
public sealed class PoiseImpactSystem : EntitySystem
{
    [Dependency] private readonly PoiseSystem _poise = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private readonly List<PendingPoiseHit> _pendingHits = new();

    private readonly record struct PendingPoiseHit(EntityUid Target, EntityUid? Shooter, EntityUid? Weapon, int Damage);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<PoiseImpactComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<PoiseImpactComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<PoiseImpactComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<PoiseImpactComponent, StartCollideEvent>(OnContact);
        SubscribeLocalEvent<PoiseImpactComponent, DelayedSpawnedEvent>(OnDelayedSpawned);
        SubscribeLocalEvent<GunComponent, HitscanResolvedEvent>(OnHitscanResolved);
    }

    private void OnAmmoShot(EntityUid uid, GunComponent gun, AmmoShotEvent args)
    {
        var weaponPoise = TryComp<PoiseDamageComponent>(uid, out var poise) ? poise.RangedPoiseDamage : null;

        // shared bolts hold their poise; the crossbow adjusts both damage types at firing time.
        TryComp<MedievalCrossbowShotModifierComponent>(uid, out var crossbow);

        // This event contains the actual bullets, including cartridge contents and spread shots.
        foreach (var projectile in args.FiredProjectiles)
        {
            // adjust the spawned bullet, not the cartridge or its reusable prototype.
            if (crossbow != null)
            {
                var damageMultiplier = MathF.Max(0f, crossbow.DamageMultiplier);

                if (TryComp<ProjectileComponent>(projectile, out var shot))
                    shot.Damage = shot.Damage * damageMultiplier;

                // Armor-piercing damage is separate from Projectile.Damage, so scale it too.
                if (TryComp<APDamageOnHitComponent>(projectile, out var armorPiercing))
                    armorPiercing.Damage = armorPiercing.Damage * damageMultiplier;
            }

            // Preserve NPC per-gun overrides. Otherwise use the base poise carried by the bolt.
            int? damage = weaponPoise;
            if (damage == null && TryComp<MedievalBoltPoiseComponent>(projectile, out var bolt))
                damage = bolt.PoiseDamage;

            if (damage == null)
            {
                // A recovered NPC arrow can subsequently be fired from an ordinary player bow.
                if (TryComp<PoiseImpactComponent>(projectile, out var old) && old.FromRangedWeapon)
                    RemComp<PoiseImpactComponent>(projectile);
                continue;
            }

            // An NPC shot uses its configured value, rather than a previous bow charge on reused ammo.
            if (TryComp<PoiseArcheryComponent>(projectile, out var arrow))
            {
                arrow.FiredPoiseDamage = 0;
                arrow.PoiseHitApplied = false;
            }

            // only player crossbow bolt poise gets the weapon multiplier.
            var poiseMultiplier = weaponPoise != null || crossbow == null
                ? 1f
                : MathF.Max(0f, crossbow.PoiseMultiplier);
            var adjustedPoise = (int)MathF.Round(
                MathF.Max(0f, damage.Value * poiseMultiplier), MidpointRounding.AwayFromZero);

            if (adjustedPoise <= 0)
            {
                RemComp<PoiseImpactComponent>(projectile);
                continue;
            }

            var impact = EnsureComp<PoiseImpactComponent>(projectile);
            impact.PoiseDamage = adjustedPoise;
            impact.Weapon = uid;
            impact.Shooter = TryComp<ProjectileComponent>(projectile, out var shotProjectile)
                ? shotProjectile.Shooter
                : TryComp<ThrownItemComponent>(projectile, out var thrown) ? thrown.Thrower : uid;
            impact.FromRangedWeapon = true;
            impact.HitTargets = new();
        }
    }

    private void OnProjectileHit(EntityUid uid, PoiseImpactComponent impact, ref ProjectileHitEvent args)
    {
        QueueImpact(uid, impact, args.Target, args.Shooter);
    }

    private void OnThrowHit(EntityUid uid, PoiseImpactComponent impact, ThrowDoHitEvent args)
    {
        // The familiar's spark is a thrown DamageOtherOnHit spell, rather than a Projectile.
        if (!HasComp<ProjectileComponent>(uid))
            QueueImpact(uid, impact, args.Target, args.Component.Thrower);
    }

    private void OnThrown(EntityUid uid, PoiseImpactComponent impact, ref ThrownEvent args)
    {
        // A new NPC gun shot binds fresh data in OnAmmoShot after the throw starts.
        // Manually throwing recovered ammunition must not retain its old NPC shot value.
        if (impact.FromRangedWeapon)
            RemComp<PoiseImpactComponent>(uid);
    }

    private void OnContact(EntityUid uid, PoiseImpactComponent impact, ref StartCollideEvent args)
    {
        if (impact.ContactFixture == null || impact.ContactFixture != args.OurFixtureId)
            return;

        if (TryComp<DamageContactsComponent>(uid, out var contacts)
            && _whitelist.IsWhitelistPass(contacts.IgnoreWhitelist, args.OtherEntity))
            return;

        QueueImpact(uid, impact, args.OtherEntity);
    }

    private void QueueImpact(EntityUid uid, PoiseImpactComponent impact, EntityUid target, EntityUid? shooter = null)
    {
        if (impact.PoiseDamage <= 0 || !HasComp<PoiseComponent>(target) || !impact.HitTargets.Add(target))
            return;

        shooter ??= impact.Shooter;
        if (shooter == null && TryComp<BossAttackComponent>(uid, out var bossAttack))
            shooter = bossAttack.Boss;

        _pendingHits.Add(new PendingPoiseHit(target, shooter ?? uid, impact.Weapon ?? uid, impact.PoiseDamage));
    }

    public void OnHitscanResolved(EntityUid uid, GunComponent gunComponent, ref HitscanResolvedEvent args)
    {
        QueueHitscan(uid, args.Target, args.Shooter);
    }

    /// <summary>Called by the server gun system after a beam resolves its final hit.</summary>
    public void QueueHitscan(EntityUid gun, EntityUid target, EntityUid? shooter)
    {
        if (!TryComp<PoiseDamageComponent>(gun, out var poise)
            || poise.RangedPoiseDamage is not { } damage || damage <= 0)
            return;

        _pendingHits.Add(new PendingPoiseHit(target, shooter, gun, damage));
    }

    private void OnDelayedSpawned(EntityUid uid, PoiseImpactComponent component, ref DelayedSpawnedEvent args)
    {
        TransferImpact(uid, args.SpawnedEntity);
    }


    /// <summary>Carry NPC shot data into delayed spell effects without changing shared player spells.</summary>
    public void TransferImpact(EntityUid source, EntityUid spawned)
    {
        if (!TryComp<PoiseImpactComponent>(source, out var original) || original.PoiseDamage <= 0)
            return;

        var impact = EnsureComp<PoiseImpactComponent>(spawned);
        impact.PoiseDamage = original.PoiseDamage;
        impact.Shooter = TryComp<ProjectileComponent>(source, out var projectile) ? projectile.Shooter : original.Shooter;
        impact.Weapon = original.Weapon;
        impact.FromRangedWeapon = original.FromRangedWeapon;
        impact.HitTargets = original.HitTargets;
        impact.ContactFixture ??= original.ContactFixture;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // As with charged arrows, defer projectile/throw poise until their hit handlers finish.
        foreach (var hit in _pendingHits)
        {
            if (!TerminatingOrDeleted(hit.Target) && HasComp<PoiseComponent>(hit.Target))
                _poise.TakePoiseDamage(hit.Target, hit.Damage, source: hit.Shooter, with: hit.Weapon);
        }

        _pendingHits.Clear();
    }
}
