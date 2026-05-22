using System.Numerics;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.MedievalNotAllowDash.Components;
using Content.Shared.Imperial.PhaseSpace;
using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.ActionBlocker;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Physics;
using Content.Shared.Physics;
using Robust.Shared.Debugging;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Standing;

namespace Content.Shared.Imperial.Dash;


public sealed partial class MedievalDashSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.MedievalDash, new PointerInputCmdHandler(DashButtonPressed))
            .Register<MedievalDashSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<MedievalDashComponent>();

        while (enumerator.MoveNext(out var uid, out var component))
        {
            if (HasComp<PhaseSpaceShadowComponent>(uid))
            {
                float curDisFromStart = Vector2.Distance(_transformSystem.GetWorldPosition(uid), component.StartDashPos);
                if (component.LegalEndDashPos != null &&
                    curDisFromStart > Vector2.Distance(component.LegalEndDashPos.Value, component.StartDashPos))
                {
                    _transformSystem.SetWorldPosition(uid, component.LegalEndDashPos.Value);
                    _physicsSystem.SetLinearVelocity(uid, Vector2.Zero);
                }
            }

            if (_timing.CurTime < component.DashEndTime) continue;
            if (!component.IsDashing) continue;

            component.IsDashing = false;
            var ev = new DashEndedEvent();
            RaiseLocalEvent(uid, ref ev);

            if (_net.IsClient) return;

            RemComp<PhaseSpaceShadowComponent>(uid);
        }
    }

    private bool DashButtonPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        return TryDash(playerSession);
    }

    private bool CanDash(EntityUid uid, [NotNullWhen(true)] out MedievalDashComponent? component)
    {
        if (!TryComp(uid, out component))
            return false;

        var isSimulationTick = _timing.CurTick == component.DashButtonPressedTick;

        if (component.IsDashing && !isSimulationTick)
            return false;

        if (_timing.CurTime < component.NextDash && !isSimulationTick)
            return false;

        if (!_actionBlockerSystem.CanMove(uid))
            return false;

        var xform = Transform(uid);
        var coords = xform.Coordinates;

        foreach (var entity in _lookup.GetEntitiesInRange(coords, 0.35f))
        {
            if (TryComp<MedievalNotAllowDashComponent>(entity, out var Dash))
            {
                return false;
            }
        }

        if (TryComp<StandingStateComponent>(uid, out var standingComp) &&
            standingComp.Standing == false &&
            _handsSystem.GetEmptyHandCount(uid) == 0)
            return false;


        var ev = new CanDashEvent();
        RaiseLocalEvent(uid, ref ev);
        return !ev.Cancelled;
    }

    private bool TryDash(ICommonSession? playerSession, Vector2? targetPos = null)
    {
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
            return false;

        if (!CanDash(player, out var component))
            return false;

        if (!TryComp<PhysicsComponent>(player, out var physicsComponent) ||
            targetPos == null && physicsComponent.LinearVelocity == Vector2.Zero)
            return false;

        if (!component.RequiredBodyStatus.Contains(physicsComponent.BodyStatus))
            return false;

        var targetRotation = targetPos == null ? physicsComponent.LinearVelocity.ToAngle() : (targetPos.Value - _transformSystem.GetWorldPosition(player)).ToAngle();

        var force = new Vector2(component.Force * 2);
        var forceDirection = targetRotation - Angle.FromDegrees(45);

        var impulse = forceDirection.RotateVec(force);
        var dashTime = TimeSpan.FromSeconds(component.Force / 990 / physicsComponent.Mass);

        var staminaEv = new CheckDashStaminaCostModifiersEvent(1f);
        RaiseLocalEvent(player, ref staminaEv);

        if (!_staminaSystem.TryTakeStamina(player, component.StaminaDamage * staminaEv.Modifier, ignoreResist: true))
            return false;

        var distEv = new CheckDashDistanceModifiersEvent(1f);
        RaiseLocalEvent(player, ref distEv);

        // TODO модификатор расстояни
        _physicsSystem.ApplyLinearImpulse(player, impulse, null, physicsComponent);

        var shadowComponent = EnsureComp<PhaseSpaceShadowComponent>(player);

        shadowComponent.ShadowUpdateRate = TimeSpan.Zero;
        shadowComponent.PositionUpdateRate = TimeSpan.Zero;
        component.DashEndTime = dashTime + _timing.CurTime;

        var cooldownEv = new CheckDashCooldownModifiersEvent(1f);
        RaiseLocalEvent(player, ref cooldownEv, true);

        component.NextDash = _timing.CurTime + component.DashReloadTime + TimeSpan.FromSeconds(staminaEv.Modifier);
        component.DashButtonPressedTick = _timing.CurTick;
        component.IsDashing = true;

        var startEv = new DashStartedEvent();
        RaiseLocalEvent(player, ref startEv);

        component.StartDashPos = _transformSystem.GetWorldPosition(player);
        component.LegalEndDashPos = _transformSystem.GetWorldPosition(player) + impulse.Normalized() * GetDashDistanceCollision(player, impulse.Normalized(), 15);

        return false;
    }

    private float? GetDashDistanceCollision(EntityUid uid, Vector2 direction, float maxDistance)
    {
        var xform = Transform(uid);
        var mask = (int)(CollisionGroup.Impassable | CollisionGroup.LowImpassable);

        for (int i = -10; i < 10; i++)
        {
            var ray = new CollisionRay(_transformSystem.GetWorldPosition(uid), Angle.FromDegrees(i).RotateVec(direction), mask);

            var results = _physicsSystem.IntersectRay(
            xform.MapID,
            ray,
            maxDistance,
            uid,
            false
        );

            foreach (var result in results)
            {
                if (result.HitEntity != EntityUid.Invalid)
                    return Math.Max(0, result.Distance - 0.35f);
            }
        }

        return null;
    }
}
