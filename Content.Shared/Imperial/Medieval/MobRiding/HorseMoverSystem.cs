using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
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
        var forwardSpeed = physics != null
            ? Vector2.Dot(physics.LinearVelocity, forward)
            : 0f;
        var wish = Vector2.Zero;
        var desiredSpeed = ev.WishDir.Length();
        var forwardHeld = (buttons & MoveButtons.Up) != 0;
        var backwardHeld = (buttons & MoveButtons.Down) != 0;
        var hasDirectionalInput = forwardHeld || backwardHeld;
        var reversingFromForward = backwardHeld && forwardSpeed > ent.Comp.MinInertiaSpeed;
        var reversingFromBackward = forwardHeld && forwardSpeed < -ent.Comp.MinInertiaSpeed;
        var conflictingInput = forwardHeld && backwardHeld;

        if (forwardHeld && !conflictingInput && !reversingFromBackward)
        {
            wish += forward;
        }

        if (backwardHeld && !conflictingInput && !reversingFromForward)
        {
            wish -= forward * ent.Comp.BackwardsModifier;
        }

        if (wish != Vector2.Zero)
        {
            wish *= desiredSpeed;
        }
        else if (forwardSpeed > ent.Comp.MinInertiaSpeed
            && (!hasDirectionalInput || reversingFromForward))
        {
            var coastFriction = ent.Comp.NoInputFriction;

            if (reversingFromForward)
                coastFriction *= ent.Comp.ReverseBrakeMultiplier;

            var coastMultiplier = Math.Clamp(1f - coastFriction * dt, 0f, 1f);
            var coastSpeed = forwardSpeed * coastMultiplier;

            if (coastSpeed > ent.Comp.MinInertiaSpeed)
                wish = forward * coastSpeed;
        }

        ev.WishDir = wish;
    }
}
