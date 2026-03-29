using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;
using NetCord;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

/// <summary>
/// This handles...
/// </summary>
public sealed class HelmSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly RDWeightSystem  _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private TimeSpan _nextCheckTime;

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
            foreach (var helmComponent in EntityManager.EntityQuery<HelmComponent>())
            {
                var helm = helmComponent.Owner;
                var boat = _transform.GetParentUid(helm);

                RotateShip(boat, helm, CheckForce(boat));
            }
        }
    }

    public void RotateShip(EntityUid boat, EntityUid helm, float helmSpeed)
    {
        _physics.WakeBody(boat);
        var helmAngle = _transform.GetWorldRotation(helm);
        var entities = _lookup.GetEntitiesIntersecting(boat);
        EntityUid? steeringOar = null;
        foreach (var e in entities)
        {
            if (HasComp<SteeringOarComponent>(e))
            {
                steeringOar = e;
                break;
            }
        }
        if (steeringOar == null)
            return;
        var steeringOarAngle = _transform.GetWorldRotation(steeringOar.Value);
        while (steeringOarAngle > 2)
            steeringOarAngle -= 2;

        while (helmAngle > 2)
            helmAngle -= 2;

        var diff = (float)steeringOarAngle*180 - (float)helmAngle*180;
        diff *= (float)0.01;
        _map.GetAllTiles(boat)
        diff *= -1;
        if (helmSpeed > 0)
        {
            _physics.ApplyAngularImpulse(boat, diff);
            return;
        }
        if (helmSpeed < 0)
        {
            _physics.ApplyAngularImpulse(boat, -diff);
            return;
        }
        Log.Info($"{diff}");
    }
    /// <summary>
    /// Проверяет скорость лодки
    /// </summary>
    /// <param name="boat"></param>
    /// <returns></returns>
    public float CheckForce(EntityUid boat)
    {
        var helmVector = _physics.GetMapLinearVelocity(boat);
        var helmForce = Math.Abs(helmVector.X) + Math.Abs(helmVector.Y);
        return helmForce;
    }
}
