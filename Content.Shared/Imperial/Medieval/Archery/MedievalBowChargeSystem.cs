
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using System.Numerics;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Content.Shared.Wieldable.Components;

namespace Content.Shared.Imperial.Medieval.Archery;

public sealed class MedievalBowChargeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _modifierSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalBowChargeComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<MedievalBowChargeComponent, ComponentShutdown>(OnShutdown);
        SubscribeAllEvent<MedievalBowChargeStart>(OnStart);
        SubscribeAllEvent<MedievalBowChargeCancel>(OnCancel);
        SubscribeAllEvent<MedievalBowChargeRelease>(OnRelease);
        SubscribeLocalEvent<MedievalBowChargeComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<MedievalBowChargeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeAttempt(EntityUid bow, MedievalBowChargeComponent charge, ref AttemptMeleeEvent args)
    {
        if (charge.IsCharging)
            args.Cancelled = true;
    }

    private void OnMeleeHit(EntityUid bow, MedievalBowChargeComponent charge, MeleeHitEvent args)
    {
        if (!_net.IsServer || !args.IsHit || args.HitEntities.Count == 0 || args.Direction != null || charge.IsCharging || charge.MeleeKnockback <= 0f)
            return;

        var userPosition = _transformSystem.GetWorldPosition(args.User);
        var userMap = Transform(args.User).MapID;

        foreach (var target in args.HitEntities)
        {
            if (target == args.User || Transform(target).MapID != userMap || !TryComp<PhysicsComponent>(target, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            var targetPosition = _transformSystem.GetWorldPosition(target);
            var direction = targetPosition - userPosition;

            if (direction.LengthSquared() <= 0.0001f)
                continue;

            var impulse = Vector2.Normalize(direction)
                * charge.MeleeKnockback
                * physics.Mass;

            _physicsSystem.ApplyLinearImpulse(target, impulse, body: physics);
        }
    }

    private void OnStart(MedievalBowChargeStart msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var bow = GetEntity(msg.Bow);

        TryStartCharging(user, bow);
    }

    private void OnCancel(MedievalBowChargeCancel msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var bow = GetEntity(msg.Bow);

        if (!TryComp<MedievalBowChargeComponent>(bow, out var charge) ||
            charge.ChargingUser != user)
            return;

        CancelCharging(bow);
    }

    private bool HasAmmo(EntityUid bow)
    {
        var ammoEvent = new GetAmmoCountEvent();
        RaiseLocalEvent(bow, ref ammoEvent);

        return ammoEvent.Count > 0;
    }

    public bool TryStartCharging(EntityUid user, EntityUid bow)
    {
        if (!TryComp<MedievalBowChargeComponent>(bow, out var charge))
            return false;

        if (charge.IsCharging)
            return false;

        if (TryComp<MeleeWeaponComponent>(bow, out var melee) && (melee.Attacking || melee.NextAttack > _timing.CurTime))
            return false;

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != bow)
            return false;

        if (!TryComp<WieldableComponent>(bow, out var wieldable) || !wieldable.Wielded)
            return false;

        if (!HasAmmo(bow))
            return false;

        if (!_combatMode.IsInCombatMode(user))
            return false;

        if (_mobState.IsDead(user) || _mobState.IsCritical(user))
            return false;

        if (charge.StaminaDrainInterval <= 0f)
            return false;

        charge.IsCharging = true;
        charge.ChargingUser = user;

        charge.ChargeStartTime = _timing.CurTime;
        charge.NextStaminaDrainTime = _timing.CurTime + TimeSpan.FromSeconds(charge.StaminaDrainInterval);

        StartEffect(bow, charge, user);
        _audioSystem.PlayPredicted(charge.DrawSound, bow, user);

        Dirty(bow, charge);
        _modifierSystem.RefreshMovementSpeedModifiers(user);
        return true;
    }

    private void OnShotAttempted(EntityUid bow, MedievalBowChargeComponent charge, ref ShotAttemptedEvent args)
    {
        if (!charge.AllowShot)
            args.Cancel();
    }

    private void StartEffect(EntityUid bow, MedievalBowChargeComponent charge, EntityUid user)
    {
        if (charge.EffectSpawnedEntity.IsValid())
            return;

        var userXform = Transform(user);

        var effect = PredictedSpawnAttachedTo(charge.ChargeEffectProtoId, userXform.Coordinates);

        _transformSystem.SetLocalRotation(effect, userXform.LocalRotation); _transformSystem.SetParent(effect, user);

        charge.EffectSpawnedEntity = effect;
        Dirty(bow, charge);
    }

    private void EndEffect(EntityUid bow, MedievalBowChargeComponent charge)
    {
        var effect = charge.EffectSpawnedEntity;

        if (!effect.IsValid())
            return;

        if (Exists(effect))
            PredictedQueueDel(effect);

        charge.EffectSpawnedEntity = EntityUid.Invalid;
        Dirty(bow, charge);
    }

    private void OnShutdown(EntityUid bow, MedievalBowChargeComponent charge, ComponentShutdown args)
    {
        if (!charge.IsCharging)
            return;

        var user = charge.ChargingUser;
        var effect = charge.EffectSpawnedEntity;

        charge.IsCharging = false;
        charge.ChargingUser = null;
        charge.AllowShot = false;
        charge.ChargeStartTime = TimeSpan.Zero;
        charge.NextStaminaDrainTime = TimeSpan.Zero;
        charge.EffectSpawnedEntity = EntityUid.Invalid;

        if (effect.IsValid() && Exists(effect))
            PredictedQueueDel(effect);

        if (user is { } uid && Exists(uid))
            _modifierSystem.RefreshMovementSpeedModifiers(uid);
    }

    public void CancelCharging(EntityUid bow)
    {
        if (!TryComp<MedievalBowChargeComponent>(bow, out var charge) || !charge.IsCharging)
            return;

        var user = charge.ChargingUser;

        charge.IsCharging = false;
        charge.ChargingUser = null;
        charge.ChargeStartTime = TimeSpan.Zero;
        charge.NextStaminaDrainTime = TimeSpan.Zero;
        charge.AllowShot = false;

        EndEffect(bow, charge);

        Dirty(bow, charge);

        if (user is { } uid && Exists(uid))
            _modifierSystem.RefreshMovementSpeedModifiers(uid);
    }

    // pure calculation for server damage and client presentation.
    public static DamageSpecifier CalculateArrowHealthDamage(
        MedievalBowChargeComponent bow, PoiseArcheryComponent arrow, float chargeMultiplier)
    {
        var damage = new DamageSpecifier();
        AddDamage(arrow.DamageType,
            arrow.FixedDamage + bow.BaseBowDamage * chargeMultiplier * arrow.DamageMultiplier);
        AddDamage("Blunt", bow.BaseBluntImpact * chargeMultiplier * arrow.BluntMultiplier);
        if (arrow.SecondaryDamageType is { } secondaryType)
        {
            AddDamage(secondaryType, arrow.SecondaryFixedDamage +
                bow.BaseBowDamage * chargeMultiplier * arrow.SecondaryDamageMultiplier);
        }
        return damage;

        void AddDamage(string type, float amount)
        {
            if (amount <= 0f)
                return;
            damage.DamageDict.TryGetValue(type, out var existing);
            damage.DamageDict[type] = existing + FixedPoint2.New(amount);
        }
    }

    public float GetChargeMultiplier(EntityUid bow)
    {
        if (!TryComp<MedievalBowChargeComponent>(bow, out var charge) || !charge.IsCharging)
            return 0f;

        var elapsed = (float)(_timing.CurTime - charge.ChargeStartTime).TotalSeconds;

        if (elapsed < charge.MinChargeTime)
            return 0f;

        if (charge.MaxChargeTime <= charge.MinChargeTime)
            return charge.MaxChargeMultiplier;

        var progress = Math.Clamp((elapsed - charge.MinChargeTime) / (charge.MaxChargeTime - charge.MinChargeTime), 0f, 1f);

        return charge.MinChargeMultiplier + (charge.MaxChargeMultiplier - charge.MinChargeMultiplier) * progress;
    }

    private void OnRelease(MedievalBowChargeRelease msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var attempted = false;
        try
        {
            var bow = GetEntity(msg.Bow);

            if (!TryComp<MedievalBowChargeComponent>(bow, out var charge) || !charge.IsCharging || charge.ChargingUser != user)
                return;

            if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != bow || !_combatMode.IsInCombatMode(user) || _mobState.IsDead(user) || _mobState.IsCritical(user) || !TryComp<GunComponent>(bow, out var gun))
            {
                CancelCharging(bow);
                return;
            }

            var multiplier = GetChargeMultiplier(bow);

            if (multiplier <= 0f)
            {
                CancelCharging(bow);
                return;
            }

            var coordinates = GetCoordinates(msg.Coordinates);

            if (_transformSystem.ToMapCoordinates(coordinates).MapId !=
                Transform(user).MapID)
            {
                CancelCharging(bow);
                return;
            }

            // Only this firing attempt is authorized.
            try
            {
                charge.AllowShot = true;

                attempted = true;
                _gunSystem.AttemptPredictedShoot(user, bow, gun, coordinates, msg.ProjectileInputTick);
            }
            finally
            {
                charge.AllowShot = false;
                CancelCharging(bow);
            }
        }
        finally
        {
            if (!attempted)
                _gunSystem.RejectProjectilePrediction(user, msg.Bow, msg.ProjectileInputTick);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalBowChargeComponent>();

        while (query.MoveNext(out var bow, out var charge))
        {
            if (!charge.IsCharging)
                continue;

            if (charge.ChargingUser is not { } user || !Exists(user))
            {
                CancelCharging(bow);
                continue;
            }

            if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != bow ||
                !TryComp<WieldableComponent>(bow, out var wieldable) ||
                !wieldable.Wielded || !_combatMode.IsInCombatMode(user) || !HasAmmo(bow) || _mobState.IsDead(user) || _mobState.IsCritical(user))
            {
                CancelCharging(bow);
                continue;
            }

            if (_timing.CurTime < charge.NextStaminaDrainTime)
                continue;

            var drainInterval = TimeSpan.FromSeconds(charge.StaminaDrainInterval);

            if (drainInterval <= TimeSpan.Zero)
            {
                CancelCharging(bow);
                continue;
            }

            var overdue = _timing.CurTime - charge.NextStaminaDrainTime;
            var drainTicks = 1 + overdue.Ticks / drainInterval.Ticks;

            var staminaCost = charge.StaminaDrainPerSecond * charge.StaminaDrainInterval * drainTicks;

            if (!_staminaSystem.TryTakeStamina(user, staminaCost, ignoreResist: true, log: false))
            {
                CancelCharging(bow);
                continue;
            }

            charge.NextStaminaDrainTime += TimeSpan.FromTicks(drainInterval.Ticks * drainTicks);

            Dirty(bow, charge);
        }
    }
}
