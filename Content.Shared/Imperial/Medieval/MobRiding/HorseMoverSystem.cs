using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.MobRiding;

public sealed class HorseMoverSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;

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

        var dt = Timing.FrameTime.TotalSeconds;

        if ((buttons & MoveButtons.Left) != 0)
            xform.LocalRotation += Angle.FromDegrees(ent.Comp.TurnSpeed * dt);

        if ((buttons & MoveButtons.Right) != 0)
            xform.LocalRotation -= Angle.FromDegrees(ent.Comp.TurnSpeed * dt);

        var forward = xform.WorldRotation.ToWorldVec();

        Vector2 wish = Vector2.Zero;

        if ((buttons & MoveButtons.Up) != 0)
            wish += forward;

        if ((buttons & MoveButtons.Down) != 0)
            wish -= forward * ent.Comp.BackwardsModifier;

        if (wish != Vector2.Zero)
            wish = wish.Normalized();

        ev.WishDir = wish;
    }
}
