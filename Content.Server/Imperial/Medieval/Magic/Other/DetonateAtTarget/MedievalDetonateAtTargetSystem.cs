using System.Numerics;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// Triggers projectiles with <see cref="MedievalDetonateAtTargetComponent"/> when they reach their target point.
/// </summary>
public sealed class MedievalDetonateAtTargetSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    // Squared distance at which the projectile counts as having arrived.
    private const float ArriveDistanceSquared = 0.04f;

    // A projectile that flew past the target is moved back only if it overshot by about one step
    private const float SnapStepFactor = 1.5f;
    private const float MinSnapDistance = 0.3f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalDetonateAtTargetComponent, PhysicsComponent>();

        while (query.MoveNext(out var uid, out var component, out var physics))
        {
            if (component.Target is not { } target)
                continue;

            var position = _transform.GetMapCoordinates(uid);

            if (position.MapId != target.MapId)
                continue;

            var toTarget = target.Position - position.Position;
            var distanceSquared = toTarget.LengthSquared();
            var arrived = distanceSquared <= ArriveDistanceSquared;
            var passed = Vector2.Dot(toTarget, physics.LinearVelocity) <= 0f;

            if (!arrived && !passed)
                continue;

            // Do not trigger twice.
            component.Target = null;

            // If it was stopped or turned around far from the target, it explodes where it is instead of jumping through obstacles
            var maxSnap = MathF.Max(MinSnapDistance, physics.LinearVelocity.Length() * frameTime * SnapStepFactor);

            if (arrived || distanceSquared <= maxSnap * maxSnap)
                _transform.SetWorldPosition(uid, target.Position);

            _trigger.Trigger(uid);
        }
    }
}
