// retain charge behavior; block overlapping attack routes and parry lockout.
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using System.Linq;
using System.Numerics;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.CombatMode;
using Content.Shared.Imperial.Medieval.Archery;

namespace Content.Shared.Imperial.Medieval.ChargedAttack;

public sealed class ChargedAttackSystem : EntitySystem
{
    [Dependency] private readonly AdvancedLightAttackSystem _advanced = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _weaponSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _modifierSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<ChargedAttackStart>(OnStart);
        SubscribeAllEvent<ChargedAttackEnd>(OnEnd);

        SubscribeLocalEvent<HumanoidAppearanceComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);

        SubscribeLocalEvent<ChargedAttackComponent, MeleeHitEvent>(OnHit);
        SubscribeLocalEvent<ChargedAttackComponent, StaminaMeleeHitEvent>(OnStaminaHit);
        SubscribeLocalEvent<ChargedAttackComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<ChargedAttackComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<ChargedAttackComponent, ToggleCombatActionEvent>(OnCombatModeToogled);
        SubscribeLocalEvent<ChargedAttackComponent, EntInsertedIntoContainerMessage>(OnInsertedIntoContainer);
    }

    private void OnCombatModeToogled(Entity<ChargedAttackComponent> weaponEntity, ref ToggleCombatActionEvent args)
    {
        if (!weaponEntity.Comp.CurrentAttacking)
            return;

        StopAttacking(weaponEntity, weaponEntity.Comp, args.Performer);
    }

    private void OnDropped(EntityUid weapon, ChargedAttackComponent charged, DroppedEvent args)
    {
        if (!charged.CurrentAttacking)
            return;

        StopAttacking(weapon, charged, args.User);
    }

    private void OnUnequipped(EntityUid weapon, ChargedAttackComponent charged, GotUnequippedHandEvent args)
    {
        if (!charged.CurrentAttacking)
            return;

        StopAttacking(weapon, charged, args.User);
    }

    private void OnInsertedIntoContainer(EntityUid weapon, ChargedAttackComponent charged, EntInsertedIntoContainerMessage args)
    {
        if (!charged.CurrentAttacking)
            return;

        var user = args.Container.Owner;

        if (TryComp<TransformComponent>(user, out var xform) && xform.ParentUid.IsValid())
        {
            if (HasComp<HandsComponent>(xform.ParentUid))
                user = xform.ParentUid;
        }

        StopAttacking(weapon, charged, user);
    }

    private void OnHit(EntityUid weapon, ChargedAttackComponent charged, MeleeHitEvent args)
    {
        // confirmed hits use their original charge, after StopAttacking has cleared the weapon.
        var modifier = args.ChargeModifierOverride ?? charged.Modifier;
        if (modifier == 0f)
            return;

        // A delayed result must not clear the state of a newer charge on this weapon.
        if (args.ChargeModifierOverride == null)
        {
            charged.Modifier = 0f;
            Dirty(weapon, charged);
        }

        if (!args.HitEntities.Any())
            return;

        args.BonusDamage = args.BaseDamage * (modifier - 1);

        var convertPiercing = args.ChargeModifierOverride != null
            ? args.ChargeConvertsPiercingToBlunt
            : charged.ConvertPiercingToBlunt;
        if (!convertPiercing)
            return;

        var piercing = args.BaseDamage.DamageDict.GetValueOrDefault("Piercing") +
                       args.BonusDamage.DamageDict.GetValueOrDefault("Piercing");

        // BonusDamage was freshly allocated above. Add the conversion to this hit
        // without changing the weapon's base damage or its cached wield bonus.
        args.BonusDamage.DamageDict["Piercing"] =
            args.BonusDamage.DamageDict.GetValueOrDefault("Piercing") - piercing;
        args.BonusDamage.DamageDict["Blunt"] =
            args.BonusDamage.DamageDict.GetValueOrDefault("Blunt") + piercing;
    }

    private void OnStaminaHit(EntityUid weapon, ChargedAttackComponent charged, StaminaMeleeHitEvent args)
    {
        if (charged.Modifier == 0f)
            return;

        var modifier = charged.Modifier;

        charged.Modifier = 0f;
        Dirty(weapon, charged);

        if (!args.HitList.Any())
            return;

        args.Multiplier *= modifier;
    }


    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ChargedAttackComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.EffectSpawnedEntity.IsValid())
                continue;

            if (comp.AttackStart + TimeSpan.FromSeconds(comp.MaxAttackTime) <= _timing.CurTime || !comp.CurrentAttacking)
            {
                EndEffect(uid, comp);
                continue;
            }

            var effect = comp.EffectSpawnedEntity;
            var parent = Transform(effect).ParentUid;
            if (!_handsSystem.TryGetActiveItem(parent, out var item) || item != uid || _mobState.IsCritical(parent) || _mobState.IsDead(parent))
            {
                EndEffect(uid, comp);
                continue;
            }
        }
    }


    private void OnRefreshMovespeed(EntityUid uid, HumanoidAppearanceComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        // Bow slowdown.
        if (_handsSystem.TryGetActiveItem(uid, out var activeItem) && TryComp<MedievalBowChargeComponent>(activeItem, out var bowCharge) && bowCharge.IsCharging && bowCharge.ChargingUser == uid)
        {
            args.ModifySpeed(bowCharge.ChargingMoveSpeedMultiplier, bowCharge.ChargingMoveSpeedMultiplier);
        }

        // either held weapon can own a committed charge.
        var speed = 1f;
        var charging = false;
        foreach (var held in _handsSystem.EnumerateHeld(uid))
        {
            if (!TryComp<ChargedAttackComponent>(held, out var heldCharge) || !heldCharge.CurrentAttacking)
                continue;
            speed = MathF.Min(speed, heldCharge.SpeedModifer);
            charging = true;
        }
        if (charging)
            args.ModifySpeed(speed, speed);
    }


    private void OnStart(ChargedAttackStart msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var weaponUid = GetEntity(msg.Weapon);
        // legacy packets cannot start an alternate timer on opted-in weapons.
        if (HasComp<AdvancedLightAttackComponent>(weaponUid))
            return;
        // a charge cannot replace this weapon's pending light attack.
        if (_advanced.IsPending(weaponUid) || _advanced.IsAttackLocked(user))
            return;

        if (!TryComp<ChargedAttackComponent>(weaponUid, out var charged))
            return;

        // a new right-click charge owns its existing release latch.
        if (TryComp<AdvancedLightAttackComponent>(weaponUid, out var advanced))
        {
            advanced.UsesPrimaryLatch = false;
            Dirty(weaponUid, advanced);
        }
        charged.AttackStart = _timing.CurTime;
        charged.CurrentAttacking = true;
        Dirty(weaponUid, charged);
        _modifierSystem.RefreshMovementSpeedModifiers(user);
        StartEffect(weaponUid, charged, user);
    }

    private void OnEnd(ChargedAttackEnd msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var coordinates = GetCoordinates(msg.Coordinates);
        var weaponUid = GetEntity(msg.Weapon);
        var attackTime = msg.AttackTime;
        // advanced strength/timing is calculated from authoritative deadlines.
        if (HasComp<AdvancedLightAttackComponent>(weaponUid))
            return;

        if (!TryComp<ChargedAttackComponent>(weaponUid, out var charged))
            return;

        if (!TryComp<MeleeWeaponComponent>(weaponUid, out var weapon))
            return;

        ChargedAttack(user, coordinates, (weaponUid, weapon), charged, attackTime, args.SenderSession);
        StopAttacking(weaponUid, charged, user);
    }

    private void ChargedAttack(EntityUid entity, EntityCoordinates coordinates, Entity<MeleeWeaponComponent> weapon, ChargedAttackComponent charged, TimeSpan attackTime, ICommonSession session)
    {
        var ratio = Math.Min((float)attackTime.TotalSeconds / charged.MaxAttackTime, 1f);
        charged.PoiseChargeRatio = ratio;
        charged.Modifier = charged.StaticModifier * ratio + 1f;
        Dirty(weapon.Owner, charged);

        if (!_staminaSystem.TryTakeStamina(entity, charged.StaminaDamage))
            return;

        _weaponSystem.AttemptHeavyAttack(entity, weapon.Owner, weapon.Comp, coordinates);

        // var direction = _transformSystem.ToMapCoordinates(coordinates).Position
        //       - _transformSystem.GetWorldPosition(entity);

        // if (direction == Vector2.Zero)
        //     return;

        // Dash(entity, charged, direction);
    }

    // private void Dash(EntityUid entity, ChargedAttackComponent charged, Vector2 direction)
    // {
    //     if (!TryComp<PhysicsComponent>(entity, out var physics))
    //         return;

    //     var impulseVector = direction.Normalized() * charged.VectorLenght * physics.Mass;
    //     _physicsSystem.SetLinearVelocity(entity, impulseVector, body: physics);
    // }

    public void OnCombatModeChanged(EntityUid user)
    {
        if (!_handsSystem.TryGetActiveItem(user, out var item) || !item.HasValue)
            return;

        if (!TryComp<ChargedAttackComponent>(item.Value, out var charged))
            return;

        if (!charged.CurrentAttacking)
            return;

        StopAttacking(item.Value, charged, user);
    }


    // the shared timeline owns commitment; this component supplies charge-specific effects.
    public void BeginAdvancedWindup(EntityUid weapon, EntityUid user, TimeSpan started)
    {
        if (!TryComp<ChargedAttackComponent>(weapon, out var charged))
            return;
        charged.AttackStart = started;
        charged.CurrentAttacking = true;
        Dirty(weapon, charged);
        _modifierSystem.RefreshMovementSpeedModifiers(user);
    }

    public void StopAttacking(EntityUid entity, ChargedAttackComponent charged, EntityUid user)
    {
        // interruptions cancel the shared pending attack without refund.
        if (TryComp<AdvancedLightAttackComponent>(entity, out var advanced) &&
            advanced.IsCharged && _advanced.IsPending(entity))
        {
            _advanced.Cancel(entity, advanced);
            return;
        }
        charged.CurrentAttacking = false;
        charged.AttackStart = TimeSpan.FromSeconds(0f);
        charged.Modifier = 0f;
        charged.PoiseChargeRatio = 0f;
        _modifierSystem.RefreshMovementSpeedModifiers(user);
        EndEffect(entity, charged);
        Dirty(entity, charged);
    }

    private void StartEffect(EntityUid entity, ChargedAttackComponent charged, EntityUid user)
    {
        var effectProtoId = charged.EffectProtoId;
        var userXform = Transform(user);
        var effect = PredictedSpawnAttachedTo(effectProtoId, userXform.Coordinates);
        _transformSystem.SetLocalRotation(effect, userXform.LocalRotation);
        _transformSystem.SetParent(effect, user);
        charged.EffectSpawnedEntity = effect;
        Dirty(entity, charged);
    }

    private void EndEffect(EntityUid entity, ChargedAttackComponent charged)
    {
        var effect = charged.EffectSpawnedEntity;
        if (!effect.Valid)
            return;

        PredictedQueueDel(effect);
        charged.EffectSpawnedEntity = EntityUid.Invalid;
        Dirty(entity, charged);
    }
}
