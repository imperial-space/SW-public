using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.MobRiding;

public sealed class HorseMoverSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HorseControlComponent, WishDirOverrideEvent>(OnWishDirOverride);
        SubscribeLocalEvent<HorseControlComponent, StopRideEvent>(OnStopRide);
    }

    public void OnWishDirOverride(Entity<HorseControlComponent> ent, ref WishDirOverrideEvent ev)
    {
        if (!TryComp<InputMoverComponent>(ent, out var mover))
            return;

        var xform = Transform(ent);

        var buttons = mover.HeldMoveButtons;

        var dt = (float) _timing.FrameTime.TotalSeconds;

        var speed = 0f;
        if (TryComp<PhysicsComponent>(ent, out var physics))
            speed = physics.LinearVelocity.Length();

        var turnSpeed = ent.Comp.TurnSpeed;

        if (speed > 0f && ent.Comp.TurnSpeedSlowdown > 0f && ent.Comp.TurnSpeedSlowdownSpeedScale > 0f)
            turnSpeed = ent.Comp.TurnSpeed / (1f + speed * ent.Comp.TurnSpeedSlowdown * ent.Comp.TurnSpeedSlowdownSpeedScale);

        if ((buttons & MoveButtons.Left) != 0)
            xform.LocalRotation += Angle.FromDegrees(turnSpeed * dt);

        if ((buttons & MoveButtons.Right) != 0)
            xform.LocalRotation -= Angle.FromDegrees(turnSpeed * dt);

        var forward = xform.WorldRotation.ToWorldVec();
        var desiredSpeed = GetDesiredSpeed(ent.Owner, mover, ev.WishDir);
        var targetThrottle = GetTargetThrottle(buttons);
        var reversing = targetThrottle != 0f
            && ent.Comp.CurrentThrottle != 0f
            && MathF.Sign(targetThrottle) != MathF.Sign(ent.Comp.CurrentThrottle);
        var throttleRate = targetThrottle == 0f || reversing
            ? ent.Comp.ThrottleDeceleration
            : ent.Comp.ThrottleAcceleration;

        ent.Comp.CurrentThrottle = Approach(ent.Comp.CurrentThrottle, targetThrottle, throttleRate * dt);

        var throttleModifier = ent.Comp.CurrentThrottle < 0f
            ? ent.Comp.BackwardsModifier
            : 1f;

        var wish = forward * ent.Comp.CurrentThrottle * throttleModifier * desiredSpeed;

        ev.WishDir = wish;
    }

    private void OnStopRide(Entity<HorseControlComponent> ent, ref StopRideEvent ev)
    {
        ent.Comp.CurrentThrottle = 0f;
    }

    private static float GetTargetThrottle(MoveButtons buttons)
    {
        var forward = (buttons & MoveButtons.Up) != 0;
        var backward = (buttons & MoveButtons.Down) != 0;

        if (forward == backward)
            return 0f;

        return forward ? 1f : -1f;
    }

    private float GetDesiredSpeed(EntityUid uid, InputMoverComponent mover, Vector2 currentWishDir)
    {
        if (currentWishDir != Vector2.Zero)
            return currentWishDir.Length();

        if (!TryComp<MovementSpeedModifierComponent>(uid, out var speed))
            return 0f;

        return mover.Sprinting ? speed.CurrentSprintSpeed : speed.CurrentWalkSpeed;
    }

    private static float Approach(float current, float target, float delta)
    {
        if (current < target)
            return MathF.Min(current + delta, target);

        return current > target
            ? MathF.Max(current - delta, target)
            : target;
    }
}
