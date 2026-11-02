using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions.Events;
using Content.Shared.Administration.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using ItemToggleMeleeWeaponComponent = Content.Shared.Item.ItemToggle.Components.ItemToggleMeleeWeaponComponent;
using Content.Shared.Imperial.Medieval.ChargedAttack;
// Imperial medieval start
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
// Imperial medieval end
using Content.Shared.Imperial.Medieval.Weapons;
using Content.Shared.Imperial.Medieval.WeaponSkillSystem; // imperial medieval

namespace Content.Shared.Weapons.Melee;

public abstract class SharedMeleeWeaponSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] private   readonly INetManager _netMan = default!;
    [Dependency] private   readonly IPrototypeManager _protoManager = default!;
    [Dependency] private   readonly IRobustRandom _random = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] private   readonly SharedHandsSystem _hands = default!;
    [Dependency] private   readonly InventorySystem _inventory = default!;
    [Dependency] private   readonly MeleeSoundSystem _meleeSound = default!;
    [Dependency] protected readonly MobStateSystem MobState = default!;
    [Dependency] private   readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedCombatModeSystem CombatMode = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] private   readonly SharedPhysicsSystem _physics = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] private   readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!; // imperial charged attack
    [Dependency] private readonly ChargedAttackSystem _chargedAttack = default!; // imperial charged attack
    [Dependency] private readonly InnerWeaponSystem _innerWeapon = default!; // imperial medieval inner weapon
    [Dependency] private readonly ImperialMeleeSystem _imperial = default!; // imperial medieval
    // Imperial medieval start
    [Dependency] private readonly AdvancedLightAttackSystem _advancedLightAttack = default!;
    // Imperial medieval end


    private const int AttackMask = (int)(CollisionGroup.MobMask | CollisionGroup.Opaque);

    /// <summary>
    /// Maximum amount of targets allowed for a wide-attack.
    /// </summary>
    public const int MaxTargets = 5;

    /// <summary>
    /// If an attack is released within this buffer it's assumed to be full damage.
    /// </summary>
    public const float GracePeriod = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(AdvancedLightAttackSystem)); // Imperial medieval edit

        SubscribeLocalEvent<MeleeWeaponComponent, HandSelectedEvent>(OnMeleeSelected);
        SubscribeLocalEvent<MeleeWeaponComponent, ShotAttemptedEvent>(OnMeleeShotAttempted);
        SubscribeLocalEvent<MeleeWeaponComponent, GunShotEvent>(OnMeleeShot);
        SubscribeLocalEvent<BonusMeleeDamageComponent, GetMeleeDamageEvent>(OnGetBonusMeleeDamage);
        SubscribeLocalEvent<BonusMeleeDamageComponent, GetHeavyDamageModifierEvent>(OnGetBonusHeavyDamageModifier);
        SubscribeLocalEvent<BonusMeleeAttackRateComponent, GetMeleeAttackRateEvent>(OnGetBonusMeleeAttackRate);

        SubscribeLocalEvent<ItemToggleMeleeWeaponComponent, ItemToggledEvent>(OnItemToggle);

        SubscribeAllEvent<HeavyAttackEvent>(OnHeavyAttack);
        SubscribeAllEvent<LightAttackEvent>(OnLightAttack);
        SubscribeAllEvent<DisarmAttackEvent>(OnDisarmAttack);
        SubscribeAllEvent<StopAttackEvent>(OnStopAttack);

#if DEBUG
        SubscribeLocalEvent<MeleeWeaponComponent,
                            MapInitEvent>                   (OnMapInit);
    }

    private void OnMapInit(EntityUid uid, MeleeWeaponComponent component, MapInitEvent args)
    {
        if (component.NextAttack > Timing.CurTime)
            Log.Warning($"Initializing a map that contains an entity that is on cooldown. Entity: {ToPrettyString(uid)}");
#endif
    }

    private void OnMeleeShotAttempted(EntityUid uid, MeleeWeaponComponent comp, ref ShotAttemptedEvent args)
    {
        if (comp.NextAttack > Timing.CurTime)
            args.Cancel();
    }

    private void OnMeleeShot(EntityUid uid, MeleeWeaponComponent component, ref GunShotEvent args)
    {
        if (!TryComp<GunComponent>(uid, out var gun))
            return;

        if (gun.NextFire > component.NextAttack)
        {
            component.NextAttack = gun.NextFire;
            DirtyField(uid, component, nameof(MeleeWeaponComponent.NextAttack));
        }
    }

    private void OnMeleeSelected(EntityUid uid, MeleeWeaponComponent component, HandSelectedEvent args)
    {
        // Imperial medieval edit - independent prepared attacks survive selecting another hand.
        // Only opted-in weapons get the dual-wield selection exception.
        if (HasComp<AdvancedLightAttackComponent>(uid) &&
            (_advancedLightAttack.IsPending(uid) || _advancedLightAttack.RefreshDualWielding(args.User)))
            return;
        var attackRate = GetAttackRate(uid, args.User, component);
        if (attackRate.Equals(0f))
            return;

        if (!component.ResetOnHandSelected)
            return;

        if (Paused(uid))
            return;

        // If someone swaps to this weapon then reset its cd.
        var curTime = Timing.CurTime;
        var minimum = curTime + TimeSpan.FromSeconds(1 / attackRate);

        if (minimum < component.NextAttack)
            return;

        component.NextAttack = minimum;
        DirtyField(uid, component, nameof(MeleeWeaponComponent.NextAttack));
    }

    private void OnGetBonusMeleeDamage(EntityUid uid, BonusMeleeDamageComponent component, ref GetMeleeDamageEvent args)
    {
        if (component.BonusDamage != null)
            args.Damage += component.BonusDamage;
        if (component.DamageModifierSet != null)
            args.Modifiers.Add(component.DamageModifierSet);
    }

    private void OnGetBonusHeavyDamageModifier(EntityUid uid, BonusMeleeDamageComponent component, ref GetHeavyDamageModifierEvent args)
    {
        args.DamageModifier += component.HeavyDamageFlatModifier;
        args.Multipliers *= component.HeavyDamageMultiplier;
    }

    private void OnGetBonusMeleeAttackRate(EntityUid uid, BonusMeleeAttackRateComponent component, ref GetMeleeAttackRateEvent args)
    {
        args.Rate += component.FlatModifier;
        args.Multipliers *= component.Multiplier;
    }

    private void OnStopAttack(StopAttackEvent msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;

        if (user == null)
            return;

        // Imperial medieval edit - a release must reach an inactive held weapon too.
        var weaponUid = GetEntity(msg.Weapon);
        if (!TryComp<MeleeWeaponComponent>(weaponUid, out var weapon))
            return;
        if (HasComp<AdvancedLightAttackComponent>(weaponUid))
        {
            if (!IsAdvancedWeaponHeldOrSelected(user.Value, weaponUid))
                return;
            _advancedLightAttack.ReleasePrimary(weaponUid);
        }
        else if (!TryGetWeapon(user.Value, out var selected, out _) || selected != weaponUid)
            return;

        if (!weapon.Attacking)
            return;

        weapon.Attacking = false;
        DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.Attacking));
    }

    private void OnLightAttack(LightAttackEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not {} user)
            return;

        if (!TryGetWeapon(user, out var weaponUid, out var weapon) ||
            weaponUid != GetEntity(msg.Weapon))
        {
            return;
        }

        AttemptAttack(user, weaponUid, weapon, msg, args.SenderSession);
    }

    private void OnHeavyAttack(HeavyAttackEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not {} user)
            return;

        if (!TryGetWeapon(user, out var weaponUid, out var weapon) ||
            weaponUid != GetEntity(msg.Weapon))
        {
            return;
        }
        // imperial charge attack start
        // Imperial medieval edit - advanced commitments can only be consumed by their timeline.
        if (!HasComp<AdvancedLightAttackComponent>(weaponUid) &&
            TryComp<ChargedAttackComponent>(weaponUid, out var comp))
        {
            _chargedAttack.StopAttacking(weaponUid, comp, user);
        }
        // imperial charge attack end

        AttemptAttack(user, weaponUid, weapon, msg, args.SenderSession);
    }

    private void OnDisarmAttack(DisarmAttackEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not {} user)
            return;

        if (TryGetWeapon(user, out var weaponUid, out var weapon))
            AttemptAttack(user, weaponUid, weapon, msg, args.SenderSession);
    }

    /// <summary>
    /// Gets the total damage a weapon does, including modifiers like wielding and enablind/disabling
    /// </summary>
    public DamageSpecifier GetDamage(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return new DamageSpecifier();

        var ev = new GetMeleeDamageEvent(uid, new(component.Damage * Damageable.UniversalMeleeDamageModifier), new(), user, component.ResistanceBypass);
        RaiseLocalEvent(uid, ref ev);

        // Imperial Medieval Skills start
        ev.RaisedOnUser = true;
        RaiseLocalEvent(user, ref ev);
        // Imperial Medieval Skills end

        return DamageSpecifier.ApplyModifierSets(ev.Damage, ev.Modifiers);
    }

    public float GetAttackRate(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0;

        var ev = new GetMeleeAttackRateEvent(uid, component.AttackRate, 1, user);
        RaiseLocalEvent(uid, ref ev);

        // Imperial Medieval Skills start
        ev.RaisedOnUser = true;
        RaiseLocalEvent(user, ref ev);
        // Imperial Medieval Skills end

        return ev.Rate * ev.Multipliers;
    }

    public FixedPoint2 GetHeavyDamageModifier(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return FixedPoint2.Zero;

        var ev = new GetHeavyDamageModifierEvent(uid, component.ClickDamageModifier, 1, user);
        RaiseLocalEvent(uid, ref ev);

        return ev.DamageModifier * ev.Multipliers;
    }

    public bool GetResistanceBypass(EntityUid uid, EntityUid user, MeleeWeaponComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var ev = new GetMeleeDamageEvent(uid, new(component.Damage * Damageable.UniversalMeleeDamageModifier), new(), user, component.ResistanceBypass);
        RaiseLocalEvent(uid, ref ev);

        return ev.ResistanceBypass;
    }

    public bool TryGetWeapon(EntityUid entity, out EntityUid weaponUid, [NotNullWhen(true)] out MeleeWeaponComponent? melee)
    {
        weaponUid = default;
        melee = null;

        var ev = new GetMeleeWeaponEvent();
        RaiseLocalEvent(entity, ev);
        if (ev.Handled)
        {
            if (TryComp(ev.Weapon, out melee))
            {
                weaponUid = ev.Weapon.Value;
                return true;
            }

            return false;
        }

        // Use inhands entity if we got one.
        if (_hands.TryGetActiveItem(entity, out var held))
        {
            // Make sure the entity is a weapon AND it doesn't need
            // to be equipped to be used (E.g boxing gloves).
            if (TryComp(held, out melee) &&
                !melee.MustBeEquippedToUse)
            {
                weaponUid = held.Value;
                return true;
            }

            if (!HasComp<VirtualItemComponent>(held))
                return false;
        }

        // Use hands clothing if applicable.
        if (_inventory.TryGetSlotEntity(entity, "gloves", out var gloves) &&
            TryComp<MeleeWeaponComponent>(gloves, out var glovesMelee))
        {
            weaponUid = gloves.Value;
            melee = glovesMelee;
            return true;
        }

        // Imperial Medieval start
        if (_innerWeapon.TryGetInnerWeapon(entity, out var inner, out _) &&
            TryComp<MeleeWeaponComponent>(inner, out var innerComp))
        {
            weaponUid = inner.Value;
            melee = innerComp;
            return true;
        }
        // Imperial Medieval end

        // Use our own melee
        if (TryComp(entity, out melee))
        {
            weaponUid = entity;
            return true;
        }

        return false;
    }

    public void AttemptLightAttackMiss(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon, EntityCoordinates coordinates)
    {
        AttemptAttack(user, weaponUid, weapon, new LightAttackEvent(null, GetNetEntity(weaponUid), GetNetCoordinates(coordinates)), null);
    }

    public bool AttemptLightAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon, EntityUid target)
    {
        if (!TryComp(target, out TransformComponent? targetXform))
            return false;

        return AttemptAttack(user, weaponUid, weapon, new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(targetXform.Coordinates)), null);
    }

    public bool AttemptDisarmAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon, EntityUid target)
    {
        if (!TryComp(target, out TransformComponent? targetXform))
            return false;

        return AttemptAttack(user, weaponUid, weapon, new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(targetXform.Coordinates)), null);
    }

    // Imperial medieval start - delayed charged attacks enter and complete through separate paths.
    // imperial charge attack start
    public bool AttemptHeavyAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon, EntityCoordinates coordinates)
    {
        // Imperial medieval edit - server/API callers receive the same minimum charge as players.
        if (HasComp<AdvancedLightAttackComponent>(weaponUid) && HasComp<ChargedAttackComponent>(weaponUid))
            return _advancedLightAttack.TryBeginCharged(user, weaponUid, weapon, GetNetCoordinates(coordinates));
        return PerformHeavyAttack(user, weaponUid, weapon, coordinates, false);
    }

    // Imperial medieval edit - server hook for a scoped historical charged strike.
    protected virtual bool PerformHeavyAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon,
        EntityCoordinates coordinates, bool completingAdvanced)
    {
        var userXform = Transform(user);
        var targetMap = TransformSystem.ToMapCoordinates(coordinates);

        if (targetMap.MapId != userXform.MapID)
            return false;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = MathF.Min(weapon.Range, direction.Length());

        var entities = GetNetEntityList(ArcRayCast(userPos, direction.ToWorldAngle(), weapon.Angle, distance, userXform.MapID, user).ToList());

        if (!_playerManager.TryGetSessionByEntity(user, out var session))
            return AttemptAttack(user, weaponUid, weapon, new HeavyAttackEvent(GetNetEntity(weaponUid), entities, GetNetCoordinates(coordinates)), null, completingAdvanced);

        return AttemptAttack(user, weaponUid, weapon, new HeavyAttackEvent(GetNetEntity(weaponUid), entities, GetNetCoordinates(coordinates)), session, completingAdvanced);
    }
    // imperial charge attack end
    // Imperial medieval end


    /// <summary>
    /// Imperial medieval edit: Attempts an immediate melee swing through the normal combat checks.
    /// </summary>
    /// <returns>True if attack successful</returns>
    private bool AttemptAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon,
        AttackEvent attack, ICommonSession? session, bool completingAdvanced = false)
    {
        var curTime = Timing.CurTime;

        // Imperial medieval edit - neither an extra click nor a heavy packet may bypass preparation.
        if (_advancedLightAttack.IsAttackLocked(user) ||
            !completingAdvanced && _advancedLightAttack.IsPending(weaponUid))
            return false;

        if (weapon.NextAttack > curTime)
            return false;

        if (!CombatMode.IsInCombatMode(user))
            return false;

        EntityUid? target = null;
        switch (attack)
        {
            case LightAttackEvent light:
                if (light.Target != null && !TryGetEntity(light.Target, out target))
                {
                    // Target was lightly attacked & deleted.
                    return false;
                }

                if (!Blocker.CanAttack(user, target, (weaponUid, weapon)))
                    return false;

                // Can't self-attack if you're the weapon
                if (weaponUid == target)
                    return false;

                break;
            case DisarmAttackEvent disarm:
                if (disarm.Target != null && !TryGetEntity(disarm.Target, out target))
                {
                    // Target was lightly attacked & deleted.
                    return false;
                }

                if (!Blocker.CanAttack(user, target, (weaponUid, weapon), true))
                    return false;
                break;
            default:
                if (!Blocker.CanAttack(user, weapon: (weaponUid, weapon)))
                    return false;
                break;
        }

        // Imperial medieval edit - preparation is separate from strike/recovery, including for AI callers.
        if (!completingAdvanced && attack is LightAttackEvent advanced && HasComp<AdvancedLightAttackComponent>(weaponUid))
            return _advancedLightAttack.TryBegin(user, weaponUid, weapon, advanced);

        // Imperial medieval edit - a direct heavy packet cannot bypass the charged minimum.
        if (!completingAdvanced && attack is HeavyAttackEvent &&
            HasComp<AdvancedLightAttackComponent>(weaponUid) && HasComp<ChargedAttackComponent>(weaponUid))
            return _advancedLightAttack.TryBeginCharged(user, weaponUid, weapon, attack.Coordinates);

        var rate = GetAttackRate(weaponUid, user, weapon);
        if (!float.IsFinite(rate) || rate <= 0f)
            return false;
        var fireRate = TimeSpan.FromSeconds(1f / rate);
        var swings = 0;

        // Imperial medieval start - advanced recovery starts at the actual strike.
        if (completingAdvanced)
        {
            weapon.NextAttack = curTime + fireRate;
            swings = 1;
        }
        else
        {
            if (weapon.NextAttack < curTime)
                weapon.NextAttack = curTime;
            while (weapon.NextAttack <= curTime)
            {
                weapon.NextAttack += fireRate;
                swings++;
            }
        }
        // Imperial medieval end

        DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.NextAttack));

        // Do this AFTER attack so it doesn't spam every tick
        // Imperial medieval edit - preserve the lance/user-aware attempt event.
        var ev = new AttemptMeleeEvent(user);
        RaiseLocalEvent(weaponUid, ref ev);
        //CrystallEdge melee improvment
        if (weapon.CPSwingBeverage)
            weapon.SwingLeft = !weapon.SwingLeft;
        //CrystallEdge melee improvment end
        if (ev.Cancelled)
        {
            if (ev.Message != null)
            {
                PopupSystem.PopupClient(ev.Message, weaponUid, user);
            }

            return false;
        }

        // Attack confirmed
        // Imperial medieval edit - wide attacks retain their own primary/secondary release behavior.
        if (attack is not LightAttackEvent && TryComp<AdvancedLightAttackComponent>(weaponUid, out var advancedLatch))
        {
            advancedLatch.UsesPrimaryLatch = false;
            Dirty(weaponUid, advancedLatch);
        }
        for (var i = 0; i < swings; i++)
        {
            string animation;

            switch (attack)
            {
                case LightAttackEvent light:
                    DoLightAttack(user, light, weaponUid, weapon, session);
                    animation = weapon.Animation;
                    break;
                case DisarmAttackEvent disarm:
                    if (!DoDisarm(user, disarm, weaponUid, weapon, session))
                        return false;

                    animation = weapon.Animation;
                    break;
                case HeavyAttackEvent heavy:
                    if (!DoHeavyAttack(user, heavy, weaponUid, weapon, session))
                        return false;

                    animation = weapon.WideAnimation;
                    break;
                default:
                    throw new NotImplementedException();
            }

            DoLungeAnimation(user, weaponUid, weapon.Angle, TransformSystem.ToMapCoordinates(GetCoordinates(attack.Coordinates)), weapon.Range, animation, completingAdvanced); // Imperial medieval edit
        }

        var attackEv = new MeleeAttackEvent(weaponUid);
        RaiseLocalEvent(user, ref attackEv);

        weapon.Attacking = true;
        DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.Attacking));
        return true;
    }

    // Imperial medieval edit: narrow hook into existing native/historical arc query.
    public HashSet<EntityUid> LightAttackArcRaycast(Vector2 position, Angle angle,
        Angle width, float range, MapId map, EntityUid user)
        => ArcRayCast(position, angle, width, range, map, user);

    // The server overrides target pose lookup inside a historical strike scope.
    public virtual bool TryGetLightAttackTargetPosition(EntityUid user, EntityUid target, out Vector2 position)
    {
        position = Vector2.Zero;
        if (!TryComp<TransformComponent>(target, out var transform) ||
            transform.MapID != Transform(user).MapID)
            return false;
        position = TransformSystem.GetMapCoordinates(target).Position;
        return true;
    }

    // Imperial medieval start
    public bool IsAdvancedWeaponHeldOrSelected(EntityUid user, EntityUid weapon)
    {
        if (user == weapon || _hands.IsHolding(user, weapon))
            return true;
        return TryGetWeapon(user, out var selected, out _) && selected == weapon;
    }

    public bool CheckAdvancedLightAttackAttempt(EntityUid user, EntityUid weapon)
    {
        var attempt = new AttemptMeleeEvent(user);
        RaiseLocalEvent(weapon, ref attempt);
        if (!attempt.Cancelled)
            return true;
        if (attempt.Message != null)
            PopupSystem.PopupClient(attempt.Message, weapon, user);
        return false;
    }

    // Server wraps this in the EXISTING charged strike history scope.
    protected virtual bool ExecuteAdvancedLightStrike(EntityUid user, EntityUid weaponUid,
        MeleeWeaponComponent weapon, LightAttackEvent attack)
    {
        if (!TryComp<AdvancedLightAttackComponent>(weaponUid, out var advanced))
            return false;
        var coords = GetCoordinates(attack.Coordinates);
        var selected = _advancedLightAttack.SelectLightRaycastTarget(user, weaponUid, weapon, coords, advanced);
        var target = selected is { } hit ? GetNetEntity(hit) : (NetEntity?) null;
        var resolved = new LightAttackEvent(target, attack.Weapon, attack.Coordinates, attack.AttackId);
        _playerManager.TryGetSessionByEntity(user, out var session);
        // The existing one-target attack path validates permission and resolves damage once.
        return AttemptAttack(user, weaponUid, weapon, resolved, session, completingAdvanced: true);
    }

    public bool CompleteAdvancedLightAttack(EntityUid user, EntityUid weaponUid,
        MeleeWeaponComponent weapon, LightAttackEvent attack)
    {
        // A public helper cannot release an unready or different commitment.
        if (!_advancedLightAttack.TryConsumeStrike(user, weaponUid, attack, out var accepted))
            return false;

        attack = accepted;
        var coordinates = accepted.Coordinates;
        if (!TryGetEntity(coordinates.NetEntity, out _) || !GetCoordinates(coordinates).IsValid(EntityManager))
            coordinates = GetNetCoordinates(Transform(user).Coordinates);
        var finalAttack = new LightAttackEvent(null, accepted.Weapon, coordinates, attack.AttackId);
        var result = ExecuteAdvancedLightStrike(user, weaponUid, weapon, finalAttack);
        _advancedLightAttack.FinishPresentation(user, weaponUid, attack.AttackId); // Imperial medieval edit
        return result;
    }
    public bool CompleteAdvancedChargedAttack(EntityUid user, EntityUid weaponUid,
        MeleeWeaponComponent weapon, LightAttackEvent attack)
    {
        if (!TryComp<ChargedAttackComponent>(weaponUid, out var charged) ||
            !_advancedLightAttack.TryConsumeStrike(user, weaponUid, attack, out var accepted, charged: true))
            return false;
        var advanced = Comp<AdvancedLightAttackComponent>(weaponUid);
        var fractions = AdvancedLightAttackSystem.GetChargeFractions(advanced.Strikes, advanced.MinimumStrike, advanced.MaximumStrike);
        charged.Modifier = 1f + charged.StaticModifier * fractions.Damage;
        charged.PoiseChargeRatio = fractions.Poise;
        Dirty(weaponUid, charged);
        var coordinates = accepted.Coordinates;
        if (!TryGetEntity(coordinates.NetEntity, out _) || !GetCoordinates(coordinates).IsValid(EntityManager))
            coordinates = GetNetCoordinates(Transform(user).Coordinates);
        // Recompute contacts at the actual strike using the latest cursor direction.
        var result = PerformHeavyAttack(user, weaponUid, weapon, GetCoordinates(coordinates), true);
        _chargedAttack.StopAttacking(weaponUid, charged, user);
        _advancedLightAttack.FinishPresentation(user, weaponUid, attack.AttackId); // Imperial medieval edit
        return result;
    }
    // Imperial medieval end

    protected abstract bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session);

    protected virtual void DoLightAttack(EntityUid user, LightAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        // If I do not come back later to fix Light Attacks being Heavy Attacks you can throw me in the spider pit -Errant
        var damage = GetDamage(meleeUid, user, component) * GetHeavyDamageModifier(meleeUid, user, component);
        // [IMPERIAL]
        var skillEffects = _imperial.GetSkillEffects(meleeUid, user);
        damage *= skillEffects.DamageMultiplier;
        // [IMPERIAL]
        var target = GetEntity(ev.Target);
        var resistanceBypass = GetResistanceBypass(meleeUid, user, component);

        // For consistency with wide attacks stuff needs damageable.
        if (Deleted(target) ||
            !HasComp<DamageableComponent>(target) ||
            !TryComp(target, out TransformComponent? targetXform) ||
            // Not in LOS.
            !InRange(user, target.Value, component.Range, session))
        {
            // Leave IsHit set to true, because the only time it's set to false
            // is when a melee weapon is examined. Misses are inferred from an
            // empty HitEntities.
            // TODO: This needs fixing
            if (meleeUid == user)
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Low,
                    $"{ToPrettyString(user):actor} melee attacked (light) using their hands and missed");
            }
            else
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Low,
                    $"{ToPrettyString(user):actor} melee attacked (light) using {ToPrettyString(meleeUid):tool} and missed");
            }
            var missEvent = new MeleeHitEvent(new List<EntityUid>(), user, meleeUid, damage, null);
            RaiseLocalEvent(meleeUid, missEvent);
            _meleeSound.PlaySwingSound(user, meleeUid, component);
            return;
        }

        // Imperial medieval start
        var targets = new List<EntityUid> { target.Value };
        var hitTarget = target.Value;
        var chargeModifier = TryComp<ChargedAttackComponent>(meleeUid, out var charge) ? charge.Modifier : 0f;
        var chargeConverts = charge?.ConvertPiercingToBlunt ?? false;
        if (TryDeferMeleeHit(user, meleeUid, targets, (confirmedTargets, poiseDamage) =>
            ResolveLightAttackHit(user, ev, meleeUid, component, hitTarget, targetXform,
                confirmedTargets, damage, resistanceBypass, skillEffects, poiseDamage, chargeModifier, chargeConverts)))
        {
            if (charge != null)
            {
                charge.Modifier = 0f;
                Dirty(meleeUid, charge);
            }
            return;
        }

        ResolveLightAttackHit(user, ev, meleeUid, component, hitTarget, targetXform,
            targets, damage, resistanceBypass, skillEffects, null);
    }

    /// <summary>Override on the server to confirm validated contact before committing hit consequences.</summary>
    protected virtual bool TryDeferMeleeHit(EntityUid user, EntityUid weapon, List<EntityUid> targets,
        Action<List<EntityUid>, int?> resolve)
    {
        return false;
    }

    private void ResolveLightAttackHit(EntityUid user, LightAttackEvent ev, EntityUid meleeUid,
        MeleeWeaponComponent component, EntityUid target, TransformComponent targetXform,
        List<EntityUid> targets, DamageSpecifier damage, bool resistanceBypass,
        ImperialMeleeSystem.ImperialSkillEffects skillEffects, int? poiseDamageOverride,
        float? chargeModifierOverride = null, bool chargeConvertsPiercingToBlunt = false)
    {
        if (!targets.Contains(target))
            return;
        // Sawmill.Debug($"Melee damage is {damage.Total} out of {component.Damage.Total}");

        // Raise event before doing damage so we can cancel damage if the event is handled
        // var hitEvent = new MeleeHitEvent(new List<EntityUid> { target }, user, meleeUid, damage, null);
        // RaiseLocalEvent(meleeUid, hitEvent);

        // if (hitEvent.Handled)
        //     return;

        // imperial medieval rideable start
        var before = new BeforeMeleeHitEvent(targets, user);
        RaiseLocalEvent(meleeUid, ref before);
        if (before.Cancelled) return;
        targets = before.HitEntities;


        var hitEvent = new MeleeHitEvent(targets, user, meleeUid, damage, null)
        {
            ChargeModifierOverride = chargeModifierOverride,
            ChargeConvertsPiercingToBlunt = chargeConvertsPiercingToBlunt
        };
        RaiseLocalEvent(meleeUid, hitEvent);

        if (hitEvent.Handled)
            return;
        // imperial medieval rideable end

        var weapon = GetEntity(ev.Weapon);

        // We skip weapon -> target interaction, as forensics system applies DNA on hit
        Interaction.DoContactInteraction(user, weapon);

        // If the user is using a long-range weapon, this probably shouldn't be happening? But I'll interpret melee as a
        // somewhat messy scuffle. See also, heavy attacks.
        Interaction.DoContactInteraction(user, target);

        // For stuff that cares about it being attacked.
        var attackedEvent = new AttackedEvent(meleeUid, user, targetXform.Coordinates)
        {
            PoiseDamageOverride = poiseDamageOverride // Imperial medieval edit
        };
        RaiseLocalEvent(target, attackedEvent);
        // [IMPERIAL] 2. Добавляем бонусный урон
        var totalBonus = hitEvent.BonusDamage + attackedEvent.BonusDamage;
        if (skillEffects.BonusDamage != null)
            totalBonus += skillEffects.BonusDamage;

        var modifiedDamage = DamageSpecifier.ApplyModifierSets(damage + totalBonus, hitEvent.ModifiersList);
        // ---------------------------------------------------------
        var damageResult = Damageable.TryChangeDamage(target, modifiedDamage, origin:user, ignoreResistances:resistanceBypass);
        // [IMPERIAL]
        var bypassResult = _imperial.ApplySkillEffects(target, user, meleeUid, skillEffects, damageResult, component);
        if ((damageResult == null || damageResult.Empty) && (bypassResult != null && !bypassResult.Empty))
        {
            damageResult = bypassResult;
        }
        // [IMPERIAL]

        if (damageResult is {Empty: false})
        {
            // If the target has stamina and is taking blunt damage, they should also take stamina damage based on their blunt to stamina factor
            if (damageResult.DamageDict.TryGetValue("Blunt", out var bluntDamage))
            {
                _stamina.TakeStaminaDamage(target, (bluntDamage * component.BluntStaminaDamageFactor).Float(), visual: false, source: user, with: meleeUid == user ? null : meleeUid);
            }

            // Imperial Medieval start
            var dealtEv = new MeleeDamageDealtEvent(target, user, weapon, damageResult);
            RaiseLocalEvent(user, ref dealtEv);
            // Imperial Medieval end

            if (meleeUid == user)
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Medium,
                    $"{ToPrettyString(user):actor} melee attacked (light) {ToPrettyString(target):subject} using their hands and dealt {damageResult.GetTotal():damage} damage");
            }
            else
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Medium,
                    $"{ToPrettyString(user):actor} melee attacked (light) {ToPrettyString(target):subject} using {ToPrettyString(meleeUid):tool} and dealt {damageResult.GetTotal():damage} damage");
            }

        }

        _meleeSound.PlayHitSound(target, user, GetHighestDamageSound(modifiedDamage, _protoManager), hitEvent.HitSoundOverride, component);

        if (damageResult?.GetTotal() > FixedPoint2.Zero)
        {
            DoDamageEffect(targets, user, targetXform);
        }
    }

    // Imperial medieval end
    protected abstract void DoDamageEffect(List<EntityUid> targets, EntityUid? user,  TransformComponent targetXform);

    private bool DoHeavyAttack(EntityUid user, HeavyAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        // TODO: This is copy-paste as fuck with DoPreciseAttack
        if (!TryComp(user, out TransformComponent? userXform))
            return false;

        var targetMap = TransformSystem.ToMapCoordinates(GetCoordinates(ev.Coordinates));

        if (targetMap.MapId != userXform.MapID)
            return false;

        // imperial medieval start
        // var userPos = TransformSystem.GetWorldPosition(userXform);
        // var direction = targetMap.Position - userPos;
        // var distance = Math.Min(component.Range, direction.Length());
        //
        // var damage = GetDamage(meleeUid, user, component);
        var entities = GetEntityList(ev.Entities);

        var before = new BeforeMeleeHitEvent(entities, user);
        RaiseLocalEvent(meleeUid, ref before);
        if (before.Cancelled) return false;
        entities = before.HitEntities;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = Math.Min(component.Range, direction.Length());

        var damage = GetDamage(meleeUid, user, component);
        var resistanceBypass = GetResistanceBypass(meleeUid, user, component);
        var skillEffects = _imperial.GetSkillEffects(meleeUid, user);
        damage *= skillEffects.DamageMultiplier;
        // imperial medieval end
        if (entities.Count == 0)
        {
            if (meleeUid == user)
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Low,
                    $"{ToPrettyString(user):actor} melee attacked (heavy) using their hands and missed");
            }
            else
            {
                AdminLogger.Add(LogType.MeleeHit,
                    LogImpact.Low,
                    $"{ToPrettyString(user):actor} melee attacked (heavy) using {ToPrettyString(meleeUid):tool} and missed");
            }

            var missEvent = new MeleeHitEvent(new List<EntityUid>(), user, meleeUid, damage, direction);
            RaiseLocalEvent(meleeUid, missEvent);

            // immediate audio feedback
            _meleeSound.PlaySwingSound(user, meleeUid, component);

            return true;
        }

        // Naughty input
        if (entities.Count > MaxTargets)
        {
            entities.RemoveRange(MaxTargets, entities.Count - MaxTargets);
        }

        // Validate client
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (ArcRaySuccessful(entities[i],
                    userPos,
                    direction.ToWorldAngle(),
                    component.Angle,
                    distance,
                    userXform.MapID,
                    user,
                    session))
            {
                continue;
            }

            // Bad input
            entities.RemoveAt(i);
        }

        var targets = new List<EntityUid>();
        var damageQuery = GetEntityQuery<DamageableComponent>();

        foreach (var entity in entities)
        {
            if (entity == user ||
                !damageQuery.HasComponent(entity))
                continue;

            targets.Add(entity);
        }

        // Imperial medieval start
        // Freeze permissions with the contact check, before any confirmation delay.
        targets.RemoveAll(entity => !Blocker.CanAttack(user, entity, (meleeUid, component)));
        var chargeModifier = TryComp<ChargedAttackComponent>(meleeUid, out var charge) ? charge.Modifier : 0f;
        var chargeConverts = charge?.ConvertPiercingToBlunt ?? false;
        if (TryDeferMeleeHit(user, meleeUid, targets, (confirmedTargets, poiseDamage) =>
            ResolveHeavyAttackHit(user, ev, meleeUid, component, confirmedTargets, direction,
                damage, resistanceBypass, skillEffects, poiseDamage, chargeModifier, chargeConverts)))
        {
            if (charge != null)
            {
                charge.Modifier = 0f;
                Dirty(meleeUid, charge);
            }
            return true;
        }

        ResolveHeavyAttackHit(user, ev, meleeUid, component, targets, direction,
            damage, resistanceBypass, skillEffects, null);
        return true;
    }

    private void ResolveHeavyAttackHit(EntityUid user, HeavyAttackEvent ev, EntityUid meleeUid,
        MeleeWeaponComponent component, List<EntityUid> targets, Vector2 direction,
        DamageSpecifier damage, bool resistanceBypass,
        ImperialMeleeSystem.ImperialSkillEffects skillEffects, int? poiseDamageOverride,
        float? chargeModifierOverride = null, bool chargeConvertsPiercingToBlunt = false)
    {
        if (targets.Count == 0)
            return;
        // Sawmill.Debug($"Melee damage is {damage.Total} out of {component.Damage.Total}");

        // Raise event before doing damage so we can cancel damage if the event is handled
        var hitEvent = new MeleeHitEvent(targets, user, meleeUid, damage, direction)
        {
            ChargeModifierOverride = chargeModifierOverride,
            ChargeConvertsPiercingToBlunt = chargeConvertsPiercingToBlunt
        };
        RaiseLocalEvent(meleeUid, hitEvent);

        if (hitEvent.Handled)
            return;

        var weapon = GetEntity(ev.Weapon);

        Interaction.DoContactInteraction(user, weapon);

        // For stuff that cares about it being attacked.
        foreach (var target in targets)
        {
            // We skip weapon -> target interaction, as forensics system applies DNA on hit

            // If the user is using a long-range weapon, this probably shouldn't be happening? But I'll interpret melee as a
            // somewhat messy scuffle. See also, light attacks.
            Interaction.DoContactInteraction(user, target);
        }

        var appliedDamage = new DamageSpecifier();

        for (var i = targets.Count - 1; i >= 0; i--)
        {
            var entity = targets[i];
            var attackedEvent = new AttackedEvent(meleeUid, user, GetCoordinates(ev.Coordinates))
            {
                PoiseDamageOverride = poiseDamageOverride // Imperial medieval edit
            };
            RaiseLocalEvent(entity, attackedEvent);
            // [IMPERIAL]
            var totalBonus = hitEvent.BonusDamage + attackedEvent.BonusDamage;
            if (skillEffects.BonusDamage != null)
                totalBonus += skillEffects.BonusDamage;

            var modifiedDamage = DamageSpecifier.ApplyModifierSets(damage + totalBonus, hitEvent.ModifiersList);
            // [IMPERIAL]

            var damageResult = Damageable.TryChangeDamage(entity, modifiedDamage, origin: user, ignoreResistances: resistanceBypass);

            if (damageResult != null && damageResult.GetTotal() > FixedPoint2.Zero)
            {
                // If the target has stamina and is taking blunt damage, they should also take stamina damage based on their blunt to stamina factor
                if (damageResult.DamageDict.TryGetValue("Blunt", out var bluntDamage))
                {
                    _stamina.TakeStaminaDamage(entity, (bluntDamage * component.BluntStaminaDamageFactor).Float(), visual: false, source: user, with: meleeUid == user ? null : meleeUid);
                }

                appliedDamage += damageResult;

                // Imperial Medieval start
                var dealtEv = new MeleeDamageDealtEvent(entity, user, weapon, damageResult);
                RaiseLocalEvent(user, ref dealtEv);
                // Imperial Medieval end


                if (meleeUid == user)
                {
                    AdminLogger.Add(LogType.MeleeHit,
                        LogImpact.Medium,
                        $"{ToPrettyString(user):actor} melee attacked (heavy) {ToPrettyString(entity):subject} using their hands and dealt {damageResult.GetTotal():damage} damage");
                }
                else
                {
                    AdminLogger.Add(LogType.MeleeHit,
                        LogImpact.Medium,
                        $"{ToPrettyString(user):actor} melee attacked (heavy) {ToPrettyString(entity):subject} using {ToPrettyString(meleeUid):tool} and dealt {damageResult.GetTotal():damage} damage");
                }
            }
        }

        if (targets.Count != 0)
        {
            var target = targets.First();
            _meleeSound.PlayHitSound(target, user, GetHighestDamageSound(appliedDamage, _protoManager), hitEvent.HitSoundOverride, component);
        }

        if (appliedDamage.GetTotal() > FixedPoint2.Zero)
        {
            DoDamageEffect(targets, user, Transform(targets[0]));
        }

    }

    // Imperial medieval end
    // Imperial medieval edit - server hook; the client retains its ordinary predicted arc.
    protected virtual HashSet<EntityUid> ArcRayCast(Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId, EntityUid ignore)
    {
        // TODO: This is pretty sucky.
        var widthRad = arcWidth;
        var increments = 1 + 35 * (int) Math.Ceiling(widthRad / (2 * Math.PI));
        var increment = widthRad / increments;
        var baseAngle = angle - widthRad / 2;

        var resSet = new HashSet<EntityUid>();

        for (var i = 0; i < increments; i++)
        {
            var castAngle = new Angle(baseAngle + increment * i);
            var res = _physics.IntersectRay(mapId,
                new CollisionRay(position,
                    castAngle.ToWorldVec(),
                    AttackMask),
                range,
                ignore,
                false)
                .ToList();

            // imperial medieval rideable start
            var ev = new RayCastSort(res);
            RaiseLocalEvent(ignore, ref ev);
            res = ev.HitEntities;
            // imperial medieval rideable end

            if (res.Count != 0)
            {
                // If there's exact distance overlap, we simply have to deal with all overlapping objects to avoid selecting randomly.
                var resChecked = res.Where(x => x.Distance.Equals(res[0].Distance));
                foreach (var r in resChecked)
                {
                    if (Interaction.InRangeUnobstructed(ignore, r.HitEntity, range + 0.1f, overlapCheck: false))
                        resSet.Add(r.HitEntity);
                }
            }
        }

        return resSet;
    }

    protected virtual bool ArcRaySuccessful(EntityUid targetUid,
        Vector2 position,
        Angle angle,
        Angle arcWidth,
        float range,
        MapId mapId,
        EntityUid ignore,
        ICommonSession? session)
    {
        // Only matters for server.
        return true;
    }


    public static string? GetHighestDamageSound(DamageSpecifier modifiedDamage, IPrototypeManager protoManager)
    {
        var groups = modifiedDamage.GetDamagePerGroup(protoManager);

        // Use group if it's exclusive, otherwise fall back to type.
        if (groups.Count == 1)
        {
            return groups.Keys.First();
        }

        var highestDamage = FixedPoint2.Zero;
        string? highestDamageType = null;

        foreach (var (type, damage) in modifiedDamage.DamageDict)
        {
            if (damage <= highestDamage)
                continue;

            highestDamageType = type;
        }

        return highestDamageType;
    }

    private float CalculateDisarmChance(EntityUid disarmer, EntityUid disarmed, EntityUid? inTargetHand, CombatModeComponent disarmerComp)
    {
        if (HasComp<DisarmProneComponent>(disarmer))
            return 1.0f;

        if (HasComp<DisarmProneComponent>(disarmed))
            return 0.0f;

        var chance = disarmerComp.BaseDisarmFailChance;

        if (inTargetHand != null && TryComp<DisarmMalusComponent>(inTargetHand, out var malus))
        {
            chance += malus.Malus;
        }

        return Math.Clamp(chance, 0f, 1f);
    }

    private bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        var target = GetEntity(ev.Target);

        if (Deleted(target) ||
            user == target)
        {
            return false;
        }


        if (MobState.IsIncapacitated(target.Value))
        {
            return false;
        }

        if (!TryComp<CombatModeComponent>(user, out var combatMode) ||
            combatMode.CanDisarm != true)
        {
            return false;
        }

        // Need hands or to be able to be shoved over.
        if (!TryComp<HandsComponent>(target, out var targetHandsComponent))
        {
            if (!TryComp<StatusEffectsComponent>(target, out var status) ||
                !status.AllowedEffects.Contains("KnockedDown"))
            {
                // Notify disarmable
                if (HasComp<MobStateComponent>(target.Value))
                    PopupSystem.PopupClient(Loc.GetString("disarm-action-disarmable", ("targetName", target.Value)), target.Value);

                return false;
            }
        }

        if (!InRange(user, target.Value, component.Range, session))
        {
            return false;
        }

        EntityUid? inTargetHand = null;

        if (_hands.TryGetActiveItem(target.Value, out var activeHeldEntity))
        {
            inTargetHand = activeHeldEntity.Value;
        }

        var attemptEvent = new DisarmAttemptEvent(target.Value, user, inTargetHand);

        if (inTargetHand != null)
        {
            RaiseLocalEvent(inTargetHand.Value, ref attemptEvent);
        }

        RaiseLocalEvent(target.Value, ref attemptEvent);

        if (attemptEvent.Cancelled)
            return false;

        var chance = CalculateDisarmChance(user, target.Value, inTargetHand, combatMode);

        // At this point we diverge
        if (_netMan.IsClient)
        {
            // Play a sound to give instant feedback; same with playing the animations
            _meleeSound.PlaySwingSound(user, meleeUid, component);
            return true;
        }

        if (_random.Prob(chance))
        {
            return false;
        }

        var eventArgs = new DisarmedEvent(target.Value, user, 1 - chance);
        RaiseLocalEvent(target.Value, ref eventArgs);

        // Nothing handled it so abort.
        if (!eventArgs.Handled)
        {
            return false;
        }

        Interaction.DoContactInteraction(user, target);
        AdminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(user):user} used disarm on {ToPrettyString(target):target}");

        AdminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(user):user} used disarm on {ToPrettyString(target):target}");

        _audio.PlayPvs(combatMode.DisarmSuccessSound, target.Value, AudioParams.Default.WithVariation(0.025f).WithVolume(5f));
        var targetEnt = Identity.Entity(target.Value, EntityManager);
        var userEnt = Identity.Entity(user, EntityManager);

        var msgOther = Loc.GetString(
            eventArgs.PopupPrefix + "popup-message-other-clients",
            ("performerName", userEnt),
            ("targetName", targetEnt));

        var msgUser = Loc.GetString(eventArgs.PopupPrefix + "popup-message-cursor", ("targetName", targetEnt));

        var filterOther = Filter.PvsExcept(user, entityManager: EntityManager);

        PopupSystem.PopupEntity(msgOther, user, filterOther, true);
        PopupSystem.PopupEntity(msgUser, target.Value, user);

        if (eventArgs.IsStunned)
        {

            PopupSystem.PopupEntity(Loc.GetString("stunned-component-disarm-success-others", ("source", userEnt), ("target", targetEnt)), targetEnt, Filter.PvsExcept(user), true, PopupType.LargeCaution);
            PopupSystem.PopupCursor(Loc.GetString("stunned-component-disarm-success", ("target", targetEnt)), user, PopupType.Large);

            AdminLogger.Add(LogType.DisarmedKnockdown, LogImpact.Medium, $"{ToPrettyString(user):user} knocked down {ToPrettyString(target):target}");
        }

        return true;
    }

    private void DoLungeAnimation(EntityUid user, EntityUid weapon, Angle angle, MapCoordinates coordinates, float length, string? animation, bool advanced) // Imperial medieval edit
    {
        // TODO: Assert that offset eyes are still okay.
        if (!TryComp(user, out TransformComponent? userXform))
            return;

        var invMatrix = TransformSystem.GetInvWorldMatrix(userXform);
        var localPos = Vector2.Transform(coordinates.Position, invMatrix);

        if (localPos.LengthSquared() <= 0f)
            return;

        localPos = userXform.LocalRotation.RotateVec(localPos);

        // We'll play the effect just short visually so it doesn't look like we should be hitting but actually aren't.
        const float bufferLength = 0.2f;
        var visualLength = length - bufferLength;

        if (localPos.Length() > visualLength)
            localPos = localPos.Normalized() * visualLength;

        // Imperial medieval edit - only advanced observer visuals bypass buffered lunge delivery.
        if (advanced && _advancedLightAttack.TryPresentAdvancedSwing(user, weapon, angle, localPos, animation))
            return;
        DoLunge(user, weapon, angle, localPos, animation);
    }

    public abstract void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true);

    /// <summary>
    /// Used to update the MeleeWeapon component on item toggle.
    /// </summary>
    private void OnItemToggle(EntityUid uid, ItemToggleMeleeWeaponComponent itemToggleMelee, ItemToggledEvent args)
    {
        if (!TryComp(uid, out MeleeWeaponComponent? meleeWeapon))
            return;

        if (args.Activated)
        {
            if (itemToggleMelee.ActivatedDamage != null)
            {
                //Setting deactivated damage to the weapon's regular value before changing it.
                itemToggleMelee.DeactivatedDamage ??= meleeWeapon.Damage;
                meleeWeapon.Damage = itemToggleMelee.ActivatedDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.Damage));
            }

            if (meleeWeapon.HitSound?.Equals(itemToggleMelee.ActivatedSoundOnHit) != true)
            {
                meleeWeapon.HitSound = itemToggleMelee.ActivatedSoundOnHit;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.HitSound));
            }

            if (itemToggleMelee.ActivatedSoundOnHitNoDamage != null)
            {
                //Setting the deactivated sound on no damage hit to the weapon's regular value before changing it.
                itemToggleMelee.DeactivatedSoundOnHitNoDamage ??= meleeWeapon.NoDamageSound;
                meleeWeapon.NoDamageSound = itemToggleMelee.ActivatedSoundOnHitNoDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.NoDamageSound));
            }

            if (itemToggleMelee.ActivatedSoundOnSwing != null)
            {
                //Setting the deactivated sound on no damage hit to the weapon's regular value before changing it.
                itemToggleMelee.DeactivatedSoundOnSwing ??= meleeWeapon.SwingSound;
                meleeWeapon.SwingSound = itemToggleMelee.ActivatedSoundOnSwing;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.SwingSound));
            }

            if (itemToggleMelee.DeactivatedSecret)
            {
                meleeWeapon.Hidden = false;
            }
        }
        else
        {
            if (itemToggleMelee.DeactivatedDamage != null)
            {
                meleeWeapon.Damage = itemToggleMelee.DeactivatedDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.Damage));
            }

            meleeWeapon.HitSound = itemToggleMelee.DeactivatedSoundOnHit;
            DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.HitSound));

            if (itemToggleMelee.DeactivatedSoundOnHitNoDamage != null)
            {
                meleeWeapon.NoDamageSound = itemToggleMelee.DeactivatedSoundOnHitNoDamage;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.NoDamageSound));
            }

            if (itemToggleMelee.DeactivatedSoundOnSwing != null)
            {
                meleeWeapon.SwingSound = itemToggleMelee.DeactivatedSoundOnSwing;
                DirtyField(uid, meleeWeapon, nameof(MeleeWeaponComponent.SwingSound));
            }

            if (itemToggleMelee.DeactivatedSecret)
            {
                meleeWeapon.Hidden = true;
            }
        }
    }
}


