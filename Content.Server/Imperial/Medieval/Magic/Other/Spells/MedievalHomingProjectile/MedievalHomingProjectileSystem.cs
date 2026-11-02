using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.MedievalHomingProjectile;


public sealed partial class MedievalHomingProjectileSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<MedievalHomingProjectileComponent, ProjectileComponent, PhysicsComponent>();

        while (enumerator.MoveNext(out var uid, out var component, out var projectile, out var physicsComponent))
        {
            if (TryDetonateAtMapTarget(uid, component, projectile))
                continue;

            if (component.NextUpdate >= _timing.CurTime) continue;

            component.NextUpdate = _timing.CurTime + component.UpdateRate;

            if (component.MapTarget == null && component.EntityTarget == null) continue;
            if (GetVectorSigns(physicsComponent.LinearVelocity) == component.Signs * -1)
            {
                component.Signs = GetVectorSigns(component.TargetCoords - _transformSystem.GetWorldPosition(uid));
                component.TargetCoords = component.EntityTarget.HasValue
                    ? GetPeakDistance(component.EntityTarget.Value, uid)
                    : GetPeakDistance(component.MapTarget!.Value, uid);
            }

            var grid = _transformSystem.GetGrid(uid);
            var gridRotation = grid.HasValue ? _transformSystem.GetWorldRotation(grid.Value) : Angle.Zero;

            var targetPosition = GetTargetCoords(component);
            var projectilePosition = _transformSystem.GetWorldPosition(uid);

            var direction = component.TargetCoords - projectilePosition;
            var rotation = component.RotateToTarget
                ? (targetPosition - projectilePosition).ToAngle()
                : physicsComponent.LinearVelocity.ToAngle();

            _transformSystem.SetLocalRotation(uid, rotation - component.RelativeAngle - gridRotation);
            _physicsSystem.ApplyLinearImpulse(uid,
                direction * component.LinearVelocityIntensy * component.AccelerationMultiplier,
                body: physicsComponent);
        }
    }

    #region Public API

    public void SetTarget(EntityUid projectile, EntityUid target)
    {
        SetTarget(projectile, target, 1.0f);
    }

    public void SetTarget(EntityUid projectile, EntityUid target, float linearVelocityIntensy)
    {
        SetTarget(projectile, target, linearVelocityIntensy, Angle.Zero);
    }

    public void SetTarget(EntityUid projectile, EntityUid target, float linearVelocityIntensy, Angle relativeAngle)
    {
        SetTarget(projectile, target, linearVelocityIntensy, relativeAngle, false);
    }

    public void SetTarget(EntityUid projectile, EntityUid target, float linearVelocityIntensy, Angle relativeAngle, bool rotateToTarget)
    {
        var component = EnsureComp<MedievalHomingProjectileComponent>(projectile);

        component.EntityTarget = target;
        component.MapTarget = null;
        component.PreviousCoordinates = null;
        component.LinearVelocityIntensy = linearVelocityIntensy;
        component.RelativeAngle = relativeAngle;
        component.RotateToTarget = rotateToTarget;

        component.TargetCoords = GetPeakDistance(target, projectile);
        component.Signs = GetVectorSigns(component.TargetCoords - _transformSystem.GetWorldPosition(projectile));
    }
    public void SetTarget(EntityUid projectile, MapCoordinates target)
    {
        SetTarget(projectile, target, 1.0f);
    }

    public void SetTarget(EntityUid projectile, MapCoordinates target, float linearVelocityIntensy)
    {
        SetTarget(projectile, target, linearVelocityIntensy, Angle.Zero);
    }

    public void SetTarget(EntityUid projectile, MapCoordinates target, float linearVelocityIntensy, Angle relativeAngle)
    {
        SetTarget(projectile, target, linearVelocityIntensy, relativeAngle, false);
    }

    public void SetTarget(EntityUid projectile, MapCoordinates target, float linearVelocityIntensy, Angle relativeAngle, bool rotateToTarget)
    {
        var component = EnsureComp<MedievalHomingProjectileComponent>(projectile);

        component.EntityTarget = null;
        component.MapTarget = target;
        component.PreviousCoordinates = _transformSystem.GetMapCoordinates(projectile);
        component.LinearVelocityIntensy = linearVelocityIntensy;
        component.RelativeAngle = relativeAngle;
        component.RotateToTarget = rotateToTarget;
        component.TargetCoords = GetPeakDistance(target, projectile);
        component.Signs = GetVectorSigns(component.TargetCoords - _transformSystem.GetWorldPosition(projectile));
    }

    #endregion

    #region Helpers

    private bool TryDetonateAtMapTarget(EntityUid uid, MedievalHomingProjectileComponent component, ProjectileComponent projectile)
    {
        if (!component.DetonateOnMapTarget || component.EntityTarget != null || component.MapTarget is not { } target)
            return false;

        var current = _transformSystem.GetMapCoordinates(uid);
        var previous = component.PreviousCoordinates;
        component.PreviousCoordinates = current;

        if (current.MapId != target.MapId)
            return false;

        var start = previous.HasValue && previous.Value.MapId == target.MapId
            ? previous.Value.Position
            : current.Position;

        // Check the traveled segment so a fast projectile cannot skip the arrival radius between ticks.
        var segment = current.Position - start;
        var lengthSquared = segment.LengthSquared();
        var fraction = lengthSquared > 0f
            ? Math.Clamp(Vector2.Dot(target.Position - start, segment) / lengthSquared, 0f, 1f)
            : 0f;
        var closest = start + segment * fraction;

        if (Vector2.DistanceSquared(closest, target.Position) > component.TargetArrivalRadius * component.TargetArrivalRadius)
            return false;

        _transformSystem.SetMapCoordinates(uid, target);
        _explosionSystem.TriggerExplosive(uid, user: projectile.Shooter);
        return true;
    }

    private Vector2 GetPeakDistance(EntityUid target, EntityUid projectile)
    {
        if (!TryComp<PhysicsComponent>(target, out var physicsComponent)) return Vector2.Zero;

        var targetPosition = _transformSystem.GetMapCoordinates(target).Position + (physicsComponent?.LinearVelocity ?? Vector2.Zero);
        var projectilePosition = _transformSystem.GetWorldPosition(projectile);

        return (targetPosition + projectilePosition) / 2;
    }

    private Vector2 GetPeakDistance(MapCoordinates target, EntityUid projectile)
    {
        var projectilePosition = _transformSystem.GetWorldPosition(projectile);

        return (target.Position + projectilePosition) / 2f;
    }

    private Vector2 GetTargetCoords(MedievalHomingProjectileComponent component)
    {
        if (component.EntityTarget == null) return component.MapTarget!.Value.Position;
        if (!HasComp<TransformComponent>(component.EntityTarget.Value)) return Vector2.Zero;

        return _transformSystem.GetMapCoordinates(component.EntityTarget.Value).Position;
    }

    private Vector2i GetVectorSigns(Vector2 vec) => new Vector2i(Math.Sign(vec.X), Math.Sign(vec.Y));

    #endregion
}
