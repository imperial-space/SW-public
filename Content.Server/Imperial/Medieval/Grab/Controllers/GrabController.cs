using System.Numerics;
using Content.Server.Imperial.Medieval.Grab.Components;
using Content.Server.Physics.Controllers;
using Content.Shared.Imperial.Medieval.Grab;
using Content.Shared.Imperial.Medieval.Grab.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

//=========================================================================
// GrabController.cs
//=========================================================================
// Purpose: Applies physical motion to grabbable entities. Handles grab
// movement impulses, rotation alignment, and velocity dampening.
// Author: rhailrake
//=========================================================================

namespace Content.Server.Imperial.Medieval.Grab.Controllers;

public sealed class GrabController : VirtualController
{
    private const float TargetDistance = 0.6f;
    private const float MaxSettleVelocity = 0.1f;
    private const float MaxSettleDistance = 0.1f;
    private const float ShutdownVelocity = 0.25f;
    private const float ShutdownDistance = 1.0f;
    private const float ShutdownMultiplier = 20.0f;

    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<GrabberComponent> _grabberQuery;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(MoverController));
        _xformQuery = GetEntityQuery<TransformComponent>();
        _grabberQuery = GetEntityQuery<GrabberComponent>();
        SubscribeLocalEvent<GrabMovingComponent, GrabStoppedEvent>(OnGrabStopped);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<GrabbableComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var grabbable, out var phys, out var grabbedXform))
        {
            if (grabbable.Grabber is not { Valid: true } grabber)
                continue;

            if (!_grabberQuery.TryGetComponent(grabber, out var grabberComp))
                continue;

            var moving = EnsureComp<GrabMovingComponent>(uid);
            moving.LastUpdate = _timing.CurTime;

            var grabberXform = _xformQuery.GetComponent(grabber);

            var grabberPos = _xform.GetWorldPosition(grabberXform);
            var grabberRot = _xform.GetWorldRotation(grabberXform);

            var offset = grabberRot.ToWorldVec() * TargetDistance;
            var targetPos = grabberPos + offset;

            var currentPos = _xform.GetWorldPosition(grabbedXform);
            var diff = targetPos - currentPos;
            var diffLength = diff.Length();

            if (diffLength < MaxSettleDistance && phys.LinearVelocity.Length() < MaxSettleVelocity)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);
                continue;
            }

            var impulseMod = MathHelper.Lerp(60f, 15f, Math.Clamp((phys.Mass - 5f) / (70f - 5f), 0f, 1f));
            var accel = diff.Normalized() * impulseMod;

            if (diffLength < ShutdownDistance && phys.LinearVelocity.Length() >= ShutdownVelocity)
            {
                var scale = (ShutdownDistance - diffLength) / ShutdownDistance;
                accel -= phys.LinearVelocity * ShutdownMultiplier * scale;
            }

            var impulse = accel * frameTime * phys.Mass;
            _physics.ApplyLinearImpulse(uid, impulse, body: phys);

            _xform.SetWorldRotation(uid, grabberRot);
        }
    }

    private void OnGrabStopped(EntityUid uid, GrabMovingComponent comp, ref GrabStoppedEvent args)
    {
        RemCompDeferred<GrabMovingComponent>(uid);
    }
}
