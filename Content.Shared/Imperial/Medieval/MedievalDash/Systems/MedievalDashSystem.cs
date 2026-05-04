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
using Content.Shared.Movement.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Serialization;
using Content.Shared.Coordinates;
using Robust.Shared.Physics.Events;

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

        SubscribeLocalEvent<MedievalDashComponent, PreventCollideEvent>(OnHit);

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
            if (_timing.CurTime > component.DashEndTime &&
                component.IsDashing)
                StopDash((uid, component));
        }
    }

    private bool DashButtonPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        if (playerSession == null)
            return false;

        if (CanDash(playerSession, coordinates, entity, out Entity<MedievalDashComponent, PhysicsComponent> entity2, out Vector2 direction, out CheckDashStaminaCostModifiersEvent staminaEvent))
        {
            Dash(entity2, direction, staminaEvent);
            return true;
        }

        return false;
    }

    private bool CanDash(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity, out Entity<MedievalDashComponent, PhysicsComponent> entity2, out Vector2 direction, out CheckDashStaminaCostModifiersEvent staminaEvent)
    {
        direction = Vector2.Zero;
        entity2 = default;
        staminaEvent = default;

        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
            return false;

        if (!TryComp<MedievalDashComponent>(player, out var dashComponent))
            return false;
        if (!TryComp<PhysicsComponent>(player, out var physicsComponent))
            return false;
        entity2 = (player, dashComponent, physicsComponent);

        var staminaEv = new CheckDashStaminaCostModifiersEvent(1f);
        RaiseLocalEvent(player, ref staminaEv);

        if (TryComp<StaminaComponent>(player, out var staminaComponent) &&
            staminaComponent.StaminaDamage / staminaComponent.CritThreshold > 0.9)
            return false;

        staminaEvent = staminaEv;

        var isSimulationTick = _timing.CurTick == dashComponent.DashButtonPressedTick;

        if (dashComponent.IsDashing && !isSimulationTick)
            return false;

        if (_timing.CurTime < dashComponent.NextDash && !isSimulationTick)
            return false;

        if (!_actionBlockerSystem.CanMove(player))
            return false;

        var xform = Transform(player);
        var coords = xform.Coordinates;

        foreach (var ent in _lookup.GetEntitiesInRange(coords, 0.35f))
            if (HasComp<MedievalNotAllowDashComponent>(ent))
                return false;

        if (TryComp<StandingStateComponent>(player, out var standingComp) &&
            standingComp.Standing == false &&
            _handsSystem.GetEmptyHandCount(player) == 0)
            return false;

        Vector2 dir = physicsComponent.LinearVelocity.Normalized();
        if (dir == Vector2.Zero)
            return false;
        direction = dir.Normalized();

        var ev = new CanDashEvent();
        RaiseLocalEvent(player, ref ev);
        return !ev.Cancelled;
    }

    private void Dash(Entity<MedievalDashComponent, PhysicsComponent> entity, Vector2 direction, CheckDashStaminaCostModifiersEvent staminaEv)
    {
        var (dashComponent, physicsComponent) = (entity.Comp1, entity.Comp2);

        var maxDistance = CountMaxDistance(entity, direction);
        direction *= maxDistance / direction.Length();

        var impulse = direction.Normalized() * dashComponent.Force * 2;

        var dashTime = TimeSpan.FromSeconds(dashComponent.Force / physicsComponent.Mass);
        dashComponent.DashEndTime = _timing.CurTime + dashTime;

        var distEv = new CheckDashDistanceModifiersEvent(1f);
        RaiseLocalEvent(entity, ref distEv);

        _physicsSystem.ApplyLinearImpulse(entity, impulse, null, physicsComponent);

        if (_net.IsServer)
            _staminaSystem.TryTakeStamina(entity, dashComponent.StaminaDamage * staminaEv.Modifier, ignoreResist: true);

        var shadowComponent = EnsureComp<PhaseSpaceShadowComponent>(entity);

        shadowComponent.ShadowUpdateRate = TimeSpan.Zero;
        shadowComponent.PositionUpdateRate = TimeSpan.Zero;

        var cooldownEv = new CheckDashCooldownModifiersEvent(1f);
        RaiseLocalEvent(entity, ref cooldownEv, true);

        dashComponent.NextDash = _timing.CurTime + dashComponent.DashReloadTime + TimeSpan.FromSeconds(staminaEv.Modifier);
        dashComponent.DashButtonPressedTick = _timing.CurTick;
        dashComponent.IsDashing = true;

        var startEv = new DashStartedEvent();
        RaiseLocalEvent(entity, ref startEv);

        Dirty(entity, dashComponent);
    }

    private float CountMaxDistance(Entity<MedievalDashComponent, PhysicsComponent> entity, Vector2 direction)
    {
        var (dashComponent, physicsComponent) = (entity.Comp1, entity.Comp2);

        if (physicsComponent.LinearDamping <= 0) // Перестраховка
            return 2;

        float predictedDistance = entity.Comp1.Force / entity.Comp2.Mass * 0.2f;

        return GetDashDistanceCollision(entity, direction, predictedDistance);
    }

    private float GetDashDistanceCollision(EntityUid uid, Vector2 direction, float maxDistance)
    {
        var xform = Transform(uid);
        var mask = (int)(CollisionGroup.Impassable | CollisionGroup.LowImpassable);

        var ray = new CollisionRay(_transformSystem.GetWorldPosition(uid), direction, mask);

        var results = _physicsSystem.IntersectRay(
            xform.MapID,
            ray,
            maxDistance,
            uid,
            false);

        foreach (var result in results)
        {
            if (result.HitEntity != EntityUid.Invalid)
                return MathF.Max(0, result.Distance - 1f);
        }

        return maxDistance;
    }

    private void OnHit(Entity<MedievalDashComponent> entity, ref PreventCollideEvent args)
    {
        if (!entity.Comp.IsDashing) return;

        if (args.OtherFixture.CollisionLayer == (int) (CollisionGroup.Impassable | CollisionGroup.LowImpassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable))
            StopDash(entity);

        args.Cancelled = false;
    }

    private void StopDash(Entity<MedievalDashComponent> entity)
    {
        entity.Comp.IsDashing = false;
        var ev = new DashEndedEvent();
        RaiseLocalEvent(entity, ref ev);

        RemComp<PhaseSpaceShadowComponent>(entity);
        Dirty(entity, entity.Comp);
    }
}
