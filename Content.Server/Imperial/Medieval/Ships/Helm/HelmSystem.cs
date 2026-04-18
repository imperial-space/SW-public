using System;
using Content.Server.Shuttles.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

public sealed class HelmSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

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
        component.HelmRotation = NormalizeHelmRotation((float) Transform(uid).LocalRotation.Degrees);
        _transform.SetLocalRotation(uid, Angle.FromDegrees(component.HelmRotation));
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
            return;
        }

        var degrees = MathF.Abs(component.HelmRotation).ToString("0.##");
        if (component.HelmRotation > 0f)
            args.PushMarkup(Loc.GetString("helm-examine-right", ("degrees", degrees)));
        else
            args.PushMarkup(Loc.GetString("helm-examine-left", ("degrees", degrees)));
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
        _transform.SetLocalRotation(helm, Angle.FromDegrees(helmComponent.HelmRotation));
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
            var boat = _transform.GetParentUid(helm);
            if (!HasComp<MapGridComponent>(boat))
                continue;

            RotateShip(boat, helmComponent);
        }
    }

    private void RotateShip(EntityUid boat, HelmComponent helmComponent)
    {
        if (TryComp<ShuttleComponent>(boat, out var shuttle) && !shuttle.Enabled)
            return;

        var steeringOars = CountSteeringOars(boat);
        if (steeringOars <= 0)
            return;

        var steeringInput = GetSteeringInput(helmComponent);
        if (MathF.Abs(steeringInput) < 0.001f)
            return;

        var weight = MathF.Max(helmComponent.MinShipWeight, _rdWeight.GetTotal(boat));
        var weightDivider = 1f + weight * 0.01f;
        var motionFactor = MathF.Max(helmComponent.MinMotionFactor, _physics.GetMapLinearVelocity(boat).Length());
        var angularImpulse = steeringInput * motionFactor * steeringOars * helmComponent.TurnImpulseScalar / weightDivider;

        _physics.WakeBody(boat);
        _physics.ApplyAngularImpulse(boat, angularImpulse);
    }

    private int CountSteeringOars(EntityUid boat)
    {
        var count = 0;
        foreach (var entity in _lookup.GetEntitiesIntersecting(boat))
        {
            if (HasComp<SteeringOarComponent>(entity))
                count++;
        }

        return count;
    }

    private static float GetSteeringInput(HelmComponent helmComponent)
    {
        var diffDegrees = helmComponent.HelmRotation;
        var maxTurnAngle = MathF.Max(1f, MathF.Abs(helmComponent.SteeringAngleForMaxTurn));
        return Math.Clamp(diffDegrees / maxTurnAngle, -1f, 1f);
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
