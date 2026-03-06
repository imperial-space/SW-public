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
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Imperial.Medieval.Ships.Wind;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Maths;

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
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WindChangeTime));

            RandomiseVind();

            var ships = new List<EntityUid>();
            var windAngle = _cfg.GetCVar(ShipsCCVars.WindRotation);
            var windForce = _cfg.GetCVar(ShipsCCVars.WindPower);
            foreach (var sailComponent in EntityManager.EntityQuery<SailComponent>())
            {
                if (sailComponent.Folded)
                    continue;

                var sailEntity = sailComponent.Owner;
                var boat = _transform.GetParentUid(sailEntity);
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
                var force = windForce * MathF.Cos(diffAngle/180) * sailComponent.SailSize;

                Push(sailEntity, force, boatAngle , push: sailComponent.Push, helm: sailComponent.Helm);
                if (!ships.Contains(boat))
                    ships.Add(boat);
            }
        }
    }

    private void Push(EntityUid sail, float windForce, Angle torque , bool push = true, bool helm = false)
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
        }

        if (helm)
            _helm.RotateShip(boat,sail, _helm.CheckForce(boat, sail));

        if (_helm.CheckForce(boat, sail) > _cfg.GetCVar(ShipsCCVars.ShipsMaxSpeed))
            return;

        _physics.ApplyLinearImpulse(boat, impulse);
    }

    private void RandomiseVind()
    {



        // ветерок сила
        var windForce = _cfg.GetCVar(ShipsCCVars.WindPower);
        if (windForce <= 0)
            windForce += _random.Next(0, 2);
        else if (windForce >= 10)
            windForce -= _random.Next(0, 2);
        else
            windForce += _random.Next(-1, 2);
        _cfg.SetCVar(ShipsCCVars.WindPower, windForce);

        // ветерок направление
        var windAngle = _cfg.GetCVar(ShipsCCVars.WindRotation);
        windAngle += _random.Next(-1, 2)*5; // ±5 градусов за шаг
        if (Math.Abs(windAngle) > 360)
            windAngle = 0;
        else if (windAngle < 0)
            windAngle += 360;

        _cfg.SetCVar(ShipsCCVars.WindRotation, windAngle);
    }
}
