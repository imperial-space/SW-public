using System;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Sail;

public sealed class SailSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<SailComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SailComponent, SailFoldEvent>(OnFold);
        SubscribeLocalEvent<SailComponent, RotateEvent>(OnRotate);
        SubscribeLocalEvent<SailComponent, ActivateInWorldEvent>(OnInteractHand);
    }

    private void OnStartup(EntityUid uid, SailComponent component, ComponentStartup args)
    {
        UpdateSailVisuals(uid, component);

        var sailXform = Transform(uid);
        if (sailXform.GridUid is not { } boat)
            return;

        if (HasComp<ImplicitRoofComponent>(boat))
            RemComp<ImplicitRoofComponent>(boat);
    }

    private void OnInteractHand(EntityUid uid, SailComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp(args.User, out ActorComponent? actor))
            return;

        args.Handled = true;
        RaiseNetworkEvent(new OpenSailMenuEvent(args.User.Id, uid.Id), actor.PlayerSession);
    }

    private void OnRotate(EntityUid uid, SailComponent component, RotateEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TransformComponent>(uid, out var transformComponent))
            return;

        var delta = args.Direction ? 45f : -45f;
        var newAngle = transformComponent.LocalRotation + Angle.FromDegrees(delta);
        _transform.SetLocalRotation(uid, newAngle);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime <= _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WindDelay));
        if (!_cfg.GetCVar(ShipsCCVars.WindEnabled))
            return;

        var windDirection = Angle.FromDegrees(_cfg.GetCVar(ShipsCCVars.WindRotation));
        var stormLevel = _cfg.GetCVar(ShipsCCVars.StormLevel);
        var windPower = _cfg.GetCVar(ShipsCCVars.WindPower);

        foreach (var sailComponent in EntityManager.EntityQuery<SailComponent>())
        {
            if (sailComponent.Folded)
                continue;

            var sailEntity = sailComponent.Owner;
            var sailXform = Transform(sailEntity);
            if (sailXform.GridUid is not { } boat)
                continue;

            if (HasComp<IslandComponent>(boat))
                continue;

            var mapUid = _transform.GetMap(boat);
            if (!mapUid.HasValue || !HasComp<SeaComponent>(mapUid.Value))
                continue;

            EnsureComp<ShipDrowningComponent>(boat);

            if (!sailComponent.Push)
            {
                _transform.SetWorldRotation(sailEntity, windDirection);
                continue;
            }

            if (TryComp<ShuttleComponent>(boat, out var shuttle) && !shuttle.Enabled)
                continue;

            if (GetShipSpeed(boat) >= _cfg.GetCVar(ShipsCCVars.ShipsMaxSpeed))
                continue;

            var sailDirection = _transform.GetWorldRotation(sailEntity);
            var shipDirection = _transform.GetWorldRotation(boat);
            var efficiency = GetEfficiencyByAngle(sailDirection, windDirection);
            var weightDivider = GetWeightDivider(boat);
            var force = stormLevel * windPower * sailComponent.SailSize * efficiency;
            var impulse = GetImpulseDirection(shipDirection) * (force / weightDivider);

            if (!TryComp<PhysicsComponent>(boat, out var body))
                continue;

            _physics.WakeBody(boat);
            _physics.ApplyLinearImpulse(boat, impulse, body: body);
        }
    }

    private float GetWeightDivider(EntityUid boat)
    {
        var weight = _rdWeight.GetTotal(boat);
        return MathF.Max(1f, 1f + weight * 0.01f);
    }

    private static float GetEfficiencyByAngle(Angle sailDirection, Angle windDirection)
    {
        var diff = MathF.Abs((float) Angle.ShortestDistance(sailDirection, windDirection).Degrees);

        if (diff <= 45f)
            return 1f;
        if (diff < 90f)
            return 0.5f;
        if (diff < 135f)
            return -0.5f;

        return -1f;
    }

    private static Vector2 GetImpulseDirection(Angle shipDirection)
    {
        return shipDirection.RotateVec(Vector2.UnitY);
    }

    private float GetShipSpeed(EntityUid boat)
    {
        return _physics.GetMapLinearVelocity(boat).Length();
    }

    private void OnFold(EntityUid uid, SailComponent component, SailFoldEvent args)
    {
        if (args.Cancelled || TerminatingOrDeleted(uid))
            return;

        component.Folded = !component.Folded;
        Dirty(uid, component);
        UpdateSailVisuals(uid, component);
        args.Handled = true;
    }

    private void UpdateSailVisuals(EntityUid uid, SailComponent component)
    {
        _appearance.SetData(uid, SailVisuals.Folded, component.Folded);
    }
}
