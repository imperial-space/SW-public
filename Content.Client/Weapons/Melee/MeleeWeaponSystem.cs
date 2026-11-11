// Imperial medieval edit - per-weapon advanced input routing; pending offhand attacks retain aim and timing.
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using System.Linq;
using Content.Client.Gameplay;
using Content.Shared.CombatMode;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.ChargedAttack;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Player;
// imperial medieval start
using Content.Shared.Imperial.Medieval.Archery;
// imperial medieval end

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    [Dependency] private readonly Content.Shared.Imperial.Medieval.Poise.MeleeLockOn.MeleeLockOnSystem _meleeLockOn = default!;
    private bool _wasCapturingMeleeTarget; // Prevent a selection click becoming an attack.

    // Called by the existing projectile targeting input handler before it closes capture.
    public void SuppressTargetingMousePress() => _wasCapturingMeleeTarget = true;

    private EntityQuery<TransformComponent> _xformQuery;

    private const string MeleeLungeKey = "melee-lunge";

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
        UpdatesOutsidePrediction = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateEffects();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var entity = entityNull.Value;
        // Imperial medieval edit - keep committed-attack release/aim updates running even while
        // the targeting overlay captures new attack input.
        var primary = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var secondary = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary);
        UpdateAdvancedHeldInput(entity, primary, secondary);

        // The existing overlay captures clicks. A selection press cannot become
        // an attack until the physical mouse buttons are released.
        var selecting = _meleeLockOn.IsTargeting(entity);
        if (selecting)
            _wasCapturingMeleeTarget = true;
        if (_wasCapturingMeleeTarget)
        {
            if (selecting || primary == BoundKeyState.Down || secondary == BoundKeyState.Down)
                return;
            _wasCapturingMeleeTarget = false;
        }

        if (!TryGetWeapon(entity, out var weaponUid, out var weapon))
            return;

        if (!CombatMode.IsInCombatMode(entity) || !Blocker.CanAttack(entity, weapon: (weaponUid, weapon)))
        {
            weapon.Attacking = false;
            return;
        }

        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var altDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary);

        // Imperial medieval start - primary latch is independent of the right-click charge latch.
        if (TryComp<AdvancedLightAttackComponent>(weaponUid, out var advanced) &&
            useDown == BoundKeyState.Down && altDown != BoundKeyState.Down &&
            !(TryComp<ChargedAttackComponent>(weaponUid, out var activeCharge) && activeCharge.CurrentAttacking) &&
            !(TryComp<GunComponent>(weaponUid, out var primaryGun) && primaryGun.UseKey))
        {
            if (!_advanced.IsAttackLocked(entity) && !_advanced.IsPending(weaponUid) &&
                weapon.NextAttack <= Timing.CurTime && (weapon.AutoAttack || !advanced.PrimaryHeld) &&
                TryGetAdvancedCursor(out var point, out var location))
                ClientLightAttack(entity, point, location, weaponUid, weapon);
            return;
        }
        // Imperial medieval end

        // Imperial medieval start - a charged press has its own latch, independent of mouse release/hand selection.
        if (TryComp<AdvancedLightAttackComponent>(weaponUid, out var advancedCharge) &&
            HasComp<ChargedAttackComponent>(weaponUid) &&
            !HasComp<MedievalBowChargeComponent>(weaponUid) &&
            !(TryComp<GunComponent>(weaponUid, out var chargeGun) && chargeGun.UseKey))
        {
            if (altDown == BoundKeyState.Down && !advancedCharge.SecondaryHeld &&
                !_advanced.IsPending(weaponUid) && !_advanced.IsAttackLocked(entity) &&
                weapon.NextAttack <= Timing.CurTime && TryGetAdvancedCursor(out _, out var chargeCoordinates))
            {
                if (advancedCharge.Attacker == entity)
                    _nextAdvancedAttackId = Math.Max(_nextAdvancedAttackId, advancedCharge.AttackId);
                if (++_nextAdvancedAttackId == 0)
                    ++_nextAdvancedAttackId;
                RaisePredictiveEvent(new AdvancedChargedAttackStartEvent(GetNetEntity(weaponUid),
                    _nextAdvancedAttackId, GetNetCoordinates(chargeCoordinates)));
            }
            return;
        }
        // Imperial medieval end

        if (weapon.AutoAttack || useDown != BoundKeyState.Down && altDown != BoundKeyState.Down)
        {
            if (weapon.Attacking)
            {
                RaisePredictiveEvent(new StopAttackEvent(GetNetEntity(weaponUid)));
            }
        }

        // Imperial medieval edit - preparation is never released by another click or charge.
        if (_advanced.IsPending(weaponUid) || _advanced.IsAttackLocked(entity))
            return;

        if (weapon.Attacking || weapon.NextAttack > Timing.CurTime)
        {
            return;
        }

        // TODO using targeted actions while combat mode is enabled should NOT trigger attacks.

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
        {
            return;
        }

        EntityCoordinates coordinates;

        if (MapManager.TryFindGridAt(mousePos, out var gridUid, out _))
        {
            coordinates = TransformSystem.ToCoordinates(gridUid, mousePos);
        }
        else
        {
            coordinates = TransformSystem.ToCoordinates(_map.GetMap(mousePos.MapId), mousePos);
        }

        // imperial medieval start
        if (TryComp<MedievalBowChargeComponent>(weaponUid, out var bowCharge))
        {
            if (!bowCharge.IsCharging && useDown == BoundKeyState.Down && altDown != BoundKeyState.Down)
            {
                ClientLightAttack(entity, mousePos, coordinates, weaponUid, weapon);
            }

            return;
        }
        // imperial medieval end


        // If the gun has AltFireComponent, it can be used to attack.
        if (TryComp<GunComponent>(weaponUid, out var gun) && gun.UseKey)
        {
            if (!TryComp<AltFireMeleeComponent>(weaponUid, out var altFireComponent) || altDown != BoundKeyState.Down)
                return;

            switch(altFireComponent.AttackType)
            {
                case AltFireAttackType.Light:
                    ClientLightAttack(entity, mousePos, coordinates, weaponUid, weapon);
                    break;

                case AltFireAttackType.Heavy:
                    ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
                    break;

                case AltFireAttackType.Disarm:
                    ClientDisarm(entity, mousePos, coordinates);
                    break;
            }

            return;
        }

        // imperial medieval charged attack start
        if (TryComp<ChargedAttackComponent>(weaponUid, out var charged))
        {
            if (!charged.CurrentAttacking)
            {
                if (altDown == BoundKeyState.Down)
                {
                    RaisePredictiveEvent(new ChargedAttackStart(GetNetEntity(weaponUid)));
                    return;
                }
                else if (useDown == BoundKeyState.Down)
                {
                    ClientLightAttack(entity, mousePos, coordinates, weaponUid, weapon);
                    return;
                }
            }
            else if (charged.AttackStart != TimeSpan.FromSeconds(0f))
            {
                var attackTime = Timing.CurTime - charged.AttackStart;
                if (altDown == BoundKeyState.Up && attackTime >= TimeSpan.FromSeconds(charged.MinAttackTime) || attackTime >= TimeSpan.FromSeconds(charged.MaxAttackTime))
                {
                    RaisePredictiveEvent(new ChargedAttackEnd(GetNetCoordinates(coordinates), GetNetEntity(weaponUid), attackTime));
                    return;
                }
                else if (altDown == BoundKeyState.Up && attackTime <= TimeSpan.FromSeconds(charged.MinAttackTime))
                {
                    if (weapon.AltDisarm && weaponUid == entity)
                    {
                        ClientDisarm(entity, mousePos, coordinates);
                        return;
                    }

                    ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
                    return;
                }
            }
        }
        else if (altDown == BoundKeyState.Down) // Heavy Attack
        {
            // If it's an unarmed attack then do a disarm
            if (weapon.AltDisarm && weaponUid == entity)
            {
                ClientDisarm(entity, mousePos, coordinates);
                return;
            }

            ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
            return;
        }
        else if (useDown == BoundKeyState.Down) // Light attack
            ClientLightAttack(entity, mousePos, coordinates, weaponUid, weapon);
        // imperial medieval charged attack end
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        var xform = Transform(target);
        var targetCoordinates = xform.Coordinates;
        var targetLocalAngle = xform.LocalRotation;

        return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range, overlapCheck: false);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        // Server never sends the event to us for predictiveeevent.
        _color.RaiseEffect(Color.Red, targets, Filter.Local());
    }

    /// <summary>
    /// Raises a heavy attack event with the relevant attacked entities.
    /// This is to avoid lag effecting the client's perspective too much.
    /// </summary>
    private void ClientHeavyAttack(EntityUid user, EntityCoordinates coordinates, EntityUid meleeUid, MeleeWeaponComponent component)
    {
        // Only run on first prediction to avoid the potential raycast entities changing.
        if (!_xformQuery.TryGetComponent(user, out var userXform) ||
            !Timing.IsFirstTimePredicted)
        {
            return;
        }

        var targetMap = TransformSystem.ToMapCoordinates(coordinates);

        if (targetMap.MapId != userXform.MapID)
            return;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = MathF.Min(component.Range, direction.Length());

        // This should really be improved. GetEntitiesInArc uses pos instead of bounding boxes.
        // Server will validate it with InRangeUnobstructed.
        var entities = GetNetEntityList(ArcRayCast(userPos, direction.ToWorldAngle(), component.Angle, distance, userXform.MapID, user).ToList());
        RaisePredictiveEvent(new HeavyAttackEvent(GetNetEntity(meleeUid), entities.GetRange(0, Math.Min(MaxTargets, entities.Count)), GetNetCoordinates(coordinates)));
    }

    private void ClientDisarm(EntityUid attacker, MapCoordinates mousePos, EntityCoordinates coordinates)
    {
        EntityUid? target = null;

        if (_stateManager.CurrentState is GameplayStateBase screen)
            target = screen.GetClickedEntity(mousePos);

        RaisePredictiveEvent(new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(coordinates)));
    }

    private void ClientLightAttack(EntityUid attacker, MapCoordinates mousePos, EntityCoordinates coordinates, EntityUid weaponUid, MeleeWeaponComponent meleeComponent)
    {
        var attackerPos = TransformSystem.GetMapCoordinates(attacker);

        if (mousePos.MapId != attackerPos.MapId ||
            !HasComp<AdvancedLightAttackComponent>(weaponUid) &&
            (attackerPos.Position - mousePos.Position).Length() > meleeComponent.Range)
            return;

        EntityUid? target = null;

        if (_stateManager.CurrentState is GameplayStateBase screen)
            target = screen.GetClickedEntity(mousePos);

        // Don't light-attack if interaction will be handling this instead
        if (Interaction.CombatModeCanHandInteract(attacker, target))
            return;

        // Imperial medieval edit - stable input identity survives state correction and cue deduplication.
        if (TryComp<AdvancedLightAttackComponent>(weaponUid, out var advanced) && advanced.Attacker == attacker)
            _nextAdvancedAttackId = Math.Max(_nextAdvancedAttackId, advanced.AttackId);
        var attackId = HasComp<AdvancedLightAttackComponent>(weaponUid) ? ++_nextAdvancedAttackId : 0;
        if (HasComp<AdvancedLightAttackComponent>(weaponUid) && attackId == 0)
            attackId = ++_nextAdvancedAttackId;
        RaisePredictiveEvent(new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(coordinates), attackId));
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var ent = GetEntity(ev.Entity);
        var entWeapon = GetEntity(ev.Weapon);

        // Entity might not have been sent by PVS.
        if (Exists(ent) && Exists(entWeapon))
            DoLunge(ent, entWeapon, ev.Angle, ev.LocalPos, ev.Animation);
    }
}
