using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Imperial.Medieval.Ships.Helm;
using Content.Server.Imperial.Medieval.Ships.ShipDrowning;
using Content.Server.Imperial.Medieval.Ships.WeatherVane;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Changeling;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Imperial.Medieval.Ships.Wind;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Ships.Sail;

/// <summary>
/// This handles...
/// </summary>
public sealed class SailSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem  _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly HelmSystem _helm = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private TimeSpan _nextCheckTime;


    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<SailComponent, SailFoldEvent>(OnFold);
        SubscribeLocalEvent<SailComponent, RotateEvent>(OnRotate);
        SubscribeLocalEvent<SailComponent, ActivateInWorldEvent>(OnInteractHand);
    }

    private void OnInteractHand(EntityUid uid, SailComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp(args.User, out ActorComponent? actor))
            return;

        args.Handled = true;
        RaiseNetworkEvent(new OpenSailMenuEvent(args.User.Id, args.Target.Id), actor.PlayerSession);
    }

    private void OnRotate(EntityUid uid, SailComponent component, RotateEvent args)
    {
        Log.Info($"принял");
        if (args.Handled || args.Cancelled)
            return;
        var sail = uid;
        if (!TryComp<TransformComponent>(sail, out var transformComponent))
            return;

        var rot = 0;
        if (args.Direction)
            rot = -45;
        else
            rot = 45;
        var newAngle = transformComponent.LocalRotation + rot;

        _transform.SetLocalRotation(sail, newAngle);
        Log.Info($"Я отработал, новый угол {newAngle} {transformComponent.LocalRotation}");
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WindDelay));
            if (!_cfg.GetCVar(ShipsCCVars.WindEnabled))
                return;
            var ships = new List<EntityUid>();
            var windAngle = _cfg.GetCVar(ShipsCCVars.WindRotation);
            var windForce = _cfg.GetCVar(ShipsCCVars.StormLevel);
            foreach (var sailComponent in EntityManager.EntityQuery<SailComponent>())
            {
                if (sailComponent.Folded)
                    continue;

                var sailEntity = sailComponent.Owner;
                var boat = _transform.GetParentUid(sailEntity);
                if (HasComp<IslandComponent>(boat))
                    continue;
                EnsureComp<ShipDrowningComponent>(boat);
                var boatAngle = new Angle();
                var entities = _lookup.GetEntitiesIntersecting(boat);
                foreach (var entity in entities)
                {
                    if (HasComp<SteeringOarComponent>(entity))
                        boatAngle = _transform.GetWorldRotation(entity);

                }

                var sailAngle = (float)_transform.GetWorldRotation(sailEntity)*180;

                while ( Math.Abs(sailAngle) > 360)
                {
                    if (sailAngle > 0)
                        sailAngle -= 360;
                    else
                        sailAngle += 360;
                }

                while ( Math.Abs(sailAngle) > 360)
                {
                    if (sailAngle > 0)
                        sailAngle -= 360;
                    else
                        sailAngle += 360;
                }

                var diffAngle = sailAngle - windAngle;
                var wind = _cfg.GetCVar(ShipsCCVars.WindPower);
                if (!HasComp < SeaComponent > (_transform.GetMap(boat)))
                    wind = 1;
                var force = windForce * MathF.Cos(diffAngle/180) * sailComponent.SailSize * wind;

                Push(sailEntity, force, boatAngle , push: sailComponent.Push);
                if (!ships.Contains(boat))
                    ships.Add(boat);
            }
        }
    }

    private void Push(EntityUid sail, float windForce, Angle torque , bool push = true)
    {
        var boat = _transform.GetParentUid(sail);

        var boatAngle = _transform.GetWorldRotation(boat);

        var weight = _rdWeight.GetTotal(boat);

        var entities = _lookup.GetEntitiesIntersecting(boat);

        if (entities.Count > 1000000)
            return;

        foreach (var entity in entities)
        {
            if (HasComp<RDWeightComponent>(entity))
                weight += _rdWeight.GetTotal(entity);
        }

        if (weight == 0)
            weight = 10;
        torque += Angle.FromDegrees(90);
        if (windForce < 0)
            windForce *= (float)0.5;
        var impulse = torque.ToVec() * windForce;
        if (!push)
        {
            _transform.SetWorldRotation(sail, Angle.FromDegrees(_cfg.GetCVar(ShipsCCVars.WindRotation)));
            return;
        }
        if (_helm.CheckForce(boat) > _cfg.GetCVar(ShipsCCVars.ShipsMaxSpeed))
            return;
        _physics.ApplyLinearImpulse(boat, impulse);
    }

    private void OnFold(EntityUid uid, SailComponent component, SailFoldEvent args)
    {
        if (args.Cancelled || TerminatingOrDeleted(uid))
            return;

        var rot = _transform.GetWorldRotation(uid);
        var coords = _transform.GetMapCoordinates(uid);
        Del(uid);
        if (component.Folded)
            Spawn("MedievalDecorShipSailReady", coords, rotation: rot);
        else
        {
            Spawn("MedievalDecorShipSailShipup", coords, rotation: rot);
        }
    }
}
