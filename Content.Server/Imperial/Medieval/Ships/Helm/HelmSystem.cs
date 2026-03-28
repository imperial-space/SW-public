using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;
using NetCord;
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
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem  _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public void RotateShip(EntityUid boat, EntityUid helm, float helmForce)
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
        var diff = (float)steeringOarAngle*180 - (float)helmAngle*180;
        diff *= -1;
        if (helmForce > 0)
        {
            _physics.ApplyAngularImpulse(boat, diff);
            return;
        }
        if (helmForce < 0)
        {
            _physics.ApplyAngularImpulse(boat, -diff);
            return;
        }
    }

    public float CheckForce(EntityUid boat, EntityUid helm)
    {
        var boatAngle = (float)_transform.GetWorldRotation(boat)*180;
        var helmcos = MathF.Cos(boatAngle);
        var helmsin = MathF.Sin(boatAngle);
        var helmVector = _physics.GetMapLinearVelocity(boat);
        var helmForce = Math.Abs(helmVector.X) + Math.Abs(helmVector.Y);
        return helmForce;
    }
}
