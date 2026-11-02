
using System;
using System.Collections.Generic;
using Content.Shared.Damage;
// show bow potential damage for the most recently loaded arrow.
using Content.Server.Imperial.Medieval.APDamage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Medieval.Archery;
using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server.Imperial.Medieval.Archery;

public sealed class PoiseArcherySystem : EntitySystem
{
    [Dependency] private readonly MedievalBowChargeSystem _bowSystem = default!;
    [Dependency] private readonly PoiseSystem _poiseSystem = default!;

    // reuse normal Damage examine formatting and modifiers.
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;


    private readonly List<PendingPoiseHit> _pendingPoiseHits = new();

    private readonly record struct PendingPoiseHit(EntityUid Target, EntityUid? Shooter, EntityUid? Weapon, int Damage);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalBowChargeComponent, TakeAmmoEvent>(OnTakeAmmo, after: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<MedievalBowChargeComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<PoiseArcheryComponent, ProjectileHitEvent>(OnProjectileHit);

        // track ammo when loaded, not when shot.
        SubscribeLocalEvent<MedievalBowChargeComponent, EntInsertedIntoContainerMessage>(OnArrowLoaded);
        SubscribeLocalEvent<MedievalBowChargeComponent, DamageExamineEvent>(OnBowDamageExamine);

    }

    // bow Damage examine uses the same calculations as firing.
    private void OnArrowLoaded(EntityUid bow, MedievalBowChargeComponent charge, EntInsertedIntoContainerMessage args)
    {
        // Only the bow's arrow slot counts as a load. Keep the last loaded
        // prototype even after that arrow is fired or removed from the bow.
        if (args.Container.ID != "projectiles" ||
            !HasComp<PoiseArcheryComponent>(args.Entity) ||
            MetaData(args.Entity).EntityPrototype?.ID is not { } prototypeId ||
            charge.LastLoadedArrow == prototypeId)
            return;

        charge.LastLoadedArrow = prototypeId;
    }

    private void OnBowDamageExamine(EntityUid bow, MedievalBowChargeComponent charge, ref DamageExamineEvent args)
    {
        if (!_prototypes.TryIndex(charge.LastLoadedArrow, out var arrowProto) ||
            !arrowProto.TryGetComponent<PoiseArcheryComponent>(out var arrow, Factory))
            return;

        // Do not duplicate or replace the existing shared bow damage calculation:
        // the firing system and the client projectile preview both use this method.
        var (damage, poiseDamage) = CalculateArrowDamage(charge, arrow, charge.MaxChargeMultiplier);

        args.Message.AddText($"Full-charge bow potential (last loaded: {arrowProto.Name}):");
        _damageExamine.AddDamageExamine(
            args.Message,
            _damageable.ApplyUniversalAllModifiers(damage * _damageable.UniversalProjectileDamageModifier),
            Loc.GetString("damage-projectile"));

        // The separate armor-piercing damage on needle arrows bypasses resistances.
        if (arrowProto.TryGetComponent<APDamageOnHitComponent>(out var armorPiercing, Factory) &&
            !armorPiercing.Damage.Empty)
        {
            args.Message.PushNewline();
            args.Message.AddText("Additional armor-piercing hit (ignores resistances):");
            _damageExamine.AddDamageExamine(
                args.Message,
                _damageable.ApplyUniversalAllModifiers(armorPiercing.Damage),
                "armor-piercing");
        }

        args.Message.PushNewline();
        args.Message.AddText($"Poise damage: {poiseDamage}");
    }

    private void OnTakeAmmo(EntityUid bow, MedievalBowChargeComponent charge, TakeAmmoEvent args)
    {
        if (!charge.IsCharging || !charge.AllowShot || charge.ChargingUser != args.User)
            return;

        var chargeMultiplier = _bowSystem.GetChargeMultiplier(bow);

        if (chargeMultiplier <= 0f)
            return;

        foreach (var (entity, _) in args.Ammo)
        {
            if (entity is not { } arrowUid || !TryComp<PoiseArcheryComponent>(arrowUid, out var arrow) || !TryComp<ProjectileComponent>(arrowUid, out var projectile))
                continue;

            // apply authoritative arrow damage.
            var (damage, poiseDamage) = CalculateArrowDamage(charge, arrow, chargeMultiplier);
            projectile.Damage = damage;
            arrow.FiredPoiseDamage = poiseDamage;

            arrow.PoiseHitApplied = false;
        }
    }

    private void OnAmmoShot(EntityUid bow, MedievalBowChargeComponent charge, AmmoShotEvent args)
    {
        // Ammo extraction or a canceled draw is not a successful attack.
        if (args.FiredProjectiles.Count == 0 || charge.ChargingUser is not { } user ||
            !TryComp<PoiseComponent>(user, out var poise) || poise.Npc ||
            poise.PoiseBroken || poise.PoiseDamage <= 0)
            return;

        _poiseSystem.DelayPoiseRecovery(user, charge.PoiseRecoveryDelayAfterAttack, poise);
    }

    // poise remains authoritative on the server.
    private static (DamageSpecifier Damage, int PoiseDamage) CalculateArrowDamage(
        MedievalBowChargeComponent charge,
        PoiseArcheryComponent arrow,
        float chargeMultiplier)
    {
        var chargeRange = charge.MaxChargeMultiplier - charge.MinChargeMultiplier;

        var progress = chargeRange <= 0f
            ? 1f
            : Math.Clamp((chargeMultiplier - charge.MinChargeMultiplier) / chargeRange, 0f, 1f);

        // same expected damage is used by the local projectile preview.
        var damage = MedievalBowChargeSystem.CalculateArrowHealthDamage(charge, arrow, chargeMultiplier);

        var poiseMultiplier = arrow.MinPoiseMultiplier + (arrow.MaxPoiseMultiplier - arrow.MinPoiseMultiplier) * progress;
        var poiseDamage = (int)MathF.Round(MathF.Max(0f, charge.BasePoiseDamage * chargeMultiplier * poiseMultiplier), MidpointRounding.AwayFromZero);

        return (damage, poiseDamage);
    }

    private void OnProjectileHit(EntityUid uid, PoiseArcheryComponent arrow, ref ProjectileHitEvent args)
    {
        if (arrow.PoiseHitApplied || arrow.FiredPoiseDamage <= 0 || !HasComp<PoiseComponent>(args.Target))
            return;

        arrow.PoiseHitApplied = true;

        var weapon = TryComp<ProjectileComponent>(uid, out var projectile)
            ? projectile.Weapon
            : null;

        _pendingPoiseHits.Add(new PendingPoiseHit(args.Target, args.Shooter, weapon, arrow.FiredPoiseDamage));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var hit in _pendingPoiseHits)
        {
            if (!Exists(hit.Target) || !HasComp<PoiseComponent>(hit.Target))
                continue;

            _poiseSystem.TakePoiseDamage(hit.Target, hit.Damage, source: hit.Shooter, with: hit.Weapon);
        }

        _pendingPoiseHits.Clear();
    }
}
