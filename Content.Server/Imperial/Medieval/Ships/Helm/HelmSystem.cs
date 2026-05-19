using System;
using Content.Server.Shuttles.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

public sealed class HelmSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<HelmComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HelmComponent, ActivateInWorldEvent>(OnInteractHand);
        SubscribeLocalEvent<HelmComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<HelmComponent, HelmActionDoAfterEvent>(OnHelmActionDoAfter);
        SubscribeNetworkEvent<HelmMenuActionEvent>(OnMenuOptionSelected);
    }

    private void OnStartup(EntityUid uid, HelmComponent component, ComponentStartup args)
    {
        component.HelmRotation = NormalizeHelmRotation(component.HelmRotation);
    }

    private void OnInteractHand(EntityUid uid, HelmComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp(args.User, out ActorComponent? actor))
            return;

        args.Handled = true;
        RaiseNetworkEvent(new OpenHelmMenuEvent(uid.Id), actor.PlayerSession);
    }

    private void OnExamine(EntityUid uid, HelmComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (component.HelmRotation == 0f)
        {
            args.PushMarkup(Loc.GetString("helm-examine-center"));
        }
        else
        {
            var degrees = MathF.Abs(component.HelmRotation).ToString("0.##");
            if (component.HelmRotation > 0f)
                args.PushMarkup(Loc.GetString("helm-examine-right", ("degrees", degrees)));
            else
                args.PushMarkup(Loc.GetString("helm-examine-left", ("degrees", degrees)));
        }

        args.PushMarkup(Loc.GetString("helm-examine-sails-efficiency", ("efficiency", FormatEfficiency(GetSailsEfficiency(uid)))));

        if (TryGetShipLoad(uid, component, out var weight, out var overloadCeil))
        {
            args.PushMarkup(Loc.GetString(
                "helm-examine-ship-load",
                ("weight", FormatWeight(weight)),
                ("overloadCeil", FormatWeight(overloadCeil))));
        }
    }

    private void OnMenuOptionSelected(HelmMenuActionEvent args, EntitySessionEventArgs session)
    {
        var player = session.SenderSession.AttachedEntity;
        if (player == null)
            return;

        var helm = new EntityUid(args.Target);
        if (!TryComp<HelmComponent>(helm, out var helmComponent))
            return;

        if (!_actionBlocker.CanInteract(player.Value, helm) ||
            !_actionBlocker.CanComplexInteract(player.Value) ||
            !_interaction.InRangeAndAccessible(player.Value, helm))
        {
            return;
        }

        TryStartHelmActionDoAfter(player.Value, helm, args.Action);
    }

    private void TryStartHelmActionDoAfter(EntityUid player, EntityUid helm, HelmMenuAction action)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, player, 0.5f, new HelmActionDoAfterEvent(action), helm, helm)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnHelmActionDoAfter(EntityUid uid, HelmComponent component, HelmActionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        ApplyHelmAction(uid, component, args.Action);
        args.Handled = true;
    }

    private void ApplyHelmAction(EntityUid helm, HelmComponent helmComponent, HelmMenuAction action)
    {
        switch (action)
        {
            case HelmMenuAction.RotateLeft:
                helmComponent.HelmRotation -= helmComponent.RotationStep;
                break;
            case HelmMenuAction.RotateRight:
                helmComponent.HelmRotation += helmComponent.RotationStep;
                break;
            case HelmMenuAction.Center:
                helmComponent.HelmRotation = 0f;
                break;
        }

        helmComponent.HelmRotation = NormalizeHelmRotation(helmComponent.HelmRotation);
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

        foreach (var helmComponent in EntityManager.EntityQuery<HelmComponent>())
        {
            var helm = helmComponent.Owner;
            var helmXform = Transform(helm);
            if (!TryGetGrid(helm, helmXform, out var boat))
                continue;

            RotateShip(boat, helmComponent);
        }
    }

    private void RotateShip(EntityUid boat, HelmComponent helmComponent)
    {
        if (TryComp<ShuttleComponent>(boat, out var shuttle) && !shuttle.Enabled)
            return;

        var steeringPower = GetSteeringPower(boat);
        if (steeringPower <= 0f)
            return;

        if (!TryComp<PhysicsComponent>(boat, out var body))
            return;

        var weight = MathF.Max(helmComponent.MinShipWeight, _rdWeight.GetTotal(boat));
        var weightDivider = 1f + weight * 0.01f;
        var steeringInput = GetSteeringInput(helmComponent);
        if (MathF.Abs(steeringInput) < 0.001f)
        {
            StabilizeShipRotation(boat, helmComponent, steeringPower, weightDivider, body);
            return;
        }

        var angularImpulse = steeringInput * helmComponent.MinMotionFactor * steeringPower * helmComponent.TurnImpulseScalar / weightDivider;

        _physics.WakeBody(boat, body: body);
        _physics.ApplyAngularImpulse(boat, angularImpulse, body: body);
    }

    private void StabilizeShipRotation(
        EntityUid boat,
        HelmComponent helmComponent,
        float steeringPower,
        float weightDivider,
        PhysicsComponent body)
    {
        var angularVelocity = body.AngularVelocity;
        if (MathF.Abs(angularVelocity) < 0.001f)
        {
            _physics.SetAngularVelocity(boat, 0f, body: body);
            return;
        }

        if (body.InvI <= 0f)
            return;

        var stabilizingImpulseMagnitude = helmComponent.MinMotionFactor * steeringPower * helmComponent.StabilizingImpulseScalar / weightDivider;
        if (stabilizingImpulseMagnitude <= 0f)
            return;

        var desiredImpulse = -MathF.Sign(angularVelocity) * stabilizingImpulseMagnitude;
        var stopImpulse = -angularVelocity / body.InvI;
        var stopNow = MathF.Abs(desiredImpulse) >= MathF.Abs(stopImpulse);
        var angularImpulse = stopNow ? stopImpulse : desiredImpulse;

        _physics.ApplyAngularImpulse(boat, angularImpulse, body: body);

        if (stopNow)
            _physics.SetAngularVelocity(boat, 0f, body: body);
    }

    private float GetSteeringPower(EntityUid boat)
    {
        var power = 0f;
        var childEnumerator = Transform(boat).ChildEnumerator;
        while (childEnumerator.MoveNext(out var entity))
        {
            if (!TryComp<SteeringOarComponent>(entity, out var steeringOar))
                continue;

            power += steeringOar.Power;
        }

        return power;
    }

    private float GetSailsEfficiency(EntityUid helm)
    {
        var helmXform = Transform(helm);
        if (!TryGetGrid(helm, helmXform, out var boat))
            return 0f;

        var efficiency = 0f;
        var sailEnumerator = EntityQueryEnumerator<SailComponent, TransformComponent>();
        while (sailEnumerator.MoveNext(out var sailUid, out var sail, out var sailXform))
        {
            if (!TryGetGrid(sailUid, sailXform, out var sailGrid) || sailGrid != boat)
                continue;

            efficiency += sail.LastSailEfficencyMod;
        }

        return efficiency;
    }

    private bool TryGetShipLoad(EntityUid helm, HelmComponent helmComponent, out float weight, out float overloadCeil)
    {
        weight = 0f;
        overloadCeil = 0f;

        var helmXform = Transform(helm);
        if (!TryGetGrid(helm, helmXform, out var boat) || !TryComp<MapGridComponent>(boat, out var mapGrid))
            return false;

        if (!TryGetOverloadCeil(boat, mapGrid, helmComponent.OverloadCeilPerTile, out overloadCeil))
            return false;

        weight = _rdWeight.GetTotal(boat);
        return true;
    }

    private bool TryGetOverloadCeil(EntityUid gridUid, MapGridComponent mapGrid, float overloadCeilPerTile, out float overloadCeil)
    {
        var totalTiles = 0;
        var allTiles = _map.GetAllTilesEnumerator(gridUid, mapGrid);
        while (allTiles.MoveNext(out _))
        {
            totalTiles++;
        }

        overloadCeil = totalTiles * overloadCeilPerTile;
        return totalTiles > 0;
    }

    private bool TryGetGrid(EntityUid uid, TransformComponent xform, out EntityUid grid)
    {
        grid = _transform.GetMoverCoordinates(uid, xform).EntityId;
        return HasComp<MapGridComponent>(grid);
    }

    private static float GetSteeringInput(HelmComponent helmComponent)
    {
        var diffDegrees = helmComponent.HelmRotation;
        var maxTurnAngle = MathF.Max(1f, MathF.Abs(helmComponent.SteeringAngleForMaxTurn));
        return Math.Clamp(-diffDegrees / maxTurnAngle, -1f, 1f);
    }

    private static string FormatEfficiency(float value)
    {
        return value.ToString("0.##");
    }

    private static string FormatWeight(float value)
    {
        return value.ToString("0.##");
    }

    private static float NormalizeHelmRotation(float helmRotation)
    {
        if (helmRotation > 180f)
            helmRotation -= 360f;

        if (helmRotation < -180f)
            helmRotation += 360f;

        return helmRotation;
    }
}
