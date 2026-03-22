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
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HorseControlComponent, WishDirOverrideEvent>(OnWishDirOverride);
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
            _transform.SetLocalRotation(ent, xform.LocalRotation + Angle.FromDegrees(turnSpeed * dt), xform);

        if ((buttons & MoveButtons.Right) != 0)
            _transform.SetLocalRotation(ent, xform.LocalRotation - Angle.FromDegrees(turnSpeed * dt), xform);

        var forward = _transform.GetWorldRotation(xform).ToWorldVec();
        var desiredSpeed = GetDesiredSpeed(ent.Owner, mover, ev.WishDir);
        var inputThrottle = GetTargetThrottle(buttons);
        var currentThrottle = 0f;

        if (physics != null && desiredSpeed > 0f && speed > 0f)
        {
            var forwardSpeed = Vector2.Dot(physics.LinearVelocity, forward);

            if (forwardSpeed >= 0f)
            {
                currentThrottle = Math.Clamp(forwardSpeed / desiredSpeed, 0f, 1f);
            }
            else
            {
                var backwardsSpeed = desiredSpeed * ent.Comp.BackwardsModifier;
                if (backwardsSpeed > 0f)
                    currentThrottle = Math.Clamp(forwardSpeed / backwardsSpeed, -1f, 0f);
            }
        }

        var targetThrottle = inputThrottle;
        if (inputThrottle != 0f
            && currentThrottle != 0f
            && MathF.Sign(currentThrottle) != MathF.Sign(inputThrottle))
        {
            targetThrottle = 0f;
        }

        var throttleRate = targetThrottle == 0f
            ? ent.Comp.ThrottleDeceleration
            : ent.Comp.ThrottleAcceleration;

        var throttle = Approach(currentThrottle, targetThrottle, throttleRate * dt);

        var throttleModifier = throttle < 0f
            ? ent.Comp.BackwardsModifier
            : 1f;

        var wish = forward * throttle * throttleModifier * desiredSpeed;

        ev.WishDir = wish;
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
