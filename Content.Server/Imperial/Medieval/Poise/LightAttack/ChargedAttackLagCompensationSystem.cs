using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using PhysicsTransform = Robust.Shared.Physics.Transform;

namespace Content.Server.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Queries historical target bounds without moving entities or their physics proxies.
/// One synchronous strike scope owns both discovery and final range validation.
/// </summary>
public sealed class ChargedAttackLagCompensationSystem : EntitySystem
{
    [Dependency] private readonly LagCompensationSystem _history = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private const int AttackMask = (int) (CollisionGroup.MobMask | CollisionGroup.Opaque);
    private readonly Stack<Strike> _strikes = new();

    /// <summary>
    /// A null session deliberately shadows an outer strike so a nested NPC or
    /// ordinary wide attack cannot inherit another attack's historical geometry.
    /// </summary>
    public IDisposable BeginStrike(EntityUid user, ICommonSession? session)
    {
        var strike = new Strike(user, session, Transform(user).MapID);
        if (session != null)
            CaptureTargets(strike, session);
        _strikes.Push(strike);
        return new StrikeScope(this, strike);
    }

    private void CaptureTargets(Strike strike, ICommonSession session)
    {
        // A current-position spatial query would omit the very targets we need
        // to recover. Visit tracked bodies once, not once per arc ray.
        var query = EntityQueryEnumerator<LagCompensationComponent, PhysicsComponent, FixturesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var body, out var fixtures, out var xform))
        {
            if (uid == strike.User || !body.CanCollide || xform.MapID != strike.Map ||
                TerminatingOrDeleted(uid))
                continue;

            var (coordinates, angle) = _history.GetCoordinatesAngle(uid, session, xform);
            if (!TryFreezePose(coordinates, angle, strike.Map, out var pose, out var mapPosition, out var worldAngle))
                continue;
            strike.Poses[uid] = pose;

            // Keep the native engine query when history falls back to the current
            // pose, or the target simply has not moved since the reported state.
            if (coordinates.Equals(xform.Coordinates) && angle.Equals(xform.LocalRotation))
                continue;

            // The engine raycasts tight fixture AABBs in their grid/map broadphase
            // frame. Computing a world AABB would enlarge a rotated grid's bounds.
            if (!TryComp<BroadphaseComponent>(coordinates.EntityId, out var broadphase) &&
                !_lookup.TryFindBroadphase(coordinates.EntityId, out broadphase))
                continue;
            var (_, broadRotation, matrix, inverse) =
                _transform.GetWorldPositionRotationMatrixWithInv(broadphase.Owner);
            var historicalTransform = new PhysicsTransform(Vector2.Transform(mapPosition, inverse), worldAngle - broadRotation);
            var bounds = new List<Box2>();
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                // ProxyCount also excludes bodies/fixtures removed from collision
                // queries (e.g. a target currently inside a container).
                if (!fixture.Hard || fixture.ProxyCount == 0 || (fixture.CollisionLayer & AttackMask) == 0)
                    continue;
                for (var child = 0; child < fixture.Shape.ChildCount; child++)
                    bounds.Add(fixture.Shape.ComputeAABB(historicalTransform, child));
            }

            if (bounds.Count != 0)
                strike.Replaced[uid] = new HistoricalBody(bounds, matrix, inverse);
        }
    }

    private bool TryFreezePose(EntityCoordinates coordinates, Angle localAngle, MapId map,
        out Pose pose, out Vector2 position, out Angle worldAngle)
    {
        pose = default;
        position = default;
        worldAngle = default;
        if (!coordinates.IsValid(EntityManager))
            return false;
        var mapPosition = _transform.ToMapCoordinates(coordinates);
        if (mapPosition.MapId != map || !float.IsFinite(mapPosition.Position.X) || !float.IsFinite(mapPosition.Position.Y) ||
            Transform(coordinates.EntityId).MapUid is not { } mapUid)
            return false;
        position = mapPosition.Position;
        worldAngle = _transform.GetWorldRotation(coordinates.EntityId) + localAngle;
        // Freeze the pose in map coordinates before any BeforeMeleeHitEvent can
        // move a parent. No live transform or fixture is changed by this query.
        pose = new Pose(_transform.ToCoordinates(mapUid, mapPosition),
            worldAngle - _transform.GetWorldRotation(mapUid));
        return true;
    }

    public bool TryArcRayCast(Vector2 position, Angle angle, Angle arcWidth, float range,
        MapId map, EntityUid user, out HashSet<EntityUid> targets)
    {
        targets = new HashSet<EntityUid>();
        if (!_strikes.TryPeek(out var strike) || strike.Session == null || strike.User != user || strike.Map != map)
            return false;
        if (!float.IsFinite(range) || range < 0f)
            return true;

        // Preserve the exact existing arc sample count and directions.
        var widthRad = arcWidth;
        var increments = 1 + 35 * (int) Math.Ceiling(widthRad / (2 * Math.PI));
        var increment = widthRad / increments;
        var baseAngle = angle - widthRad / 2;
        for (var i = 0; i < increments; i++)
        {
            var ray = new CollisionRay(position, new Angle(baseAngle + increment * i).ToWorldVec(), AttackMask);
            var results = _physics.IntersectRayWithPredicate(map, ray, range,
                entity => entity == user || strike.Replaced.ContainsKey(entity), false).ToList();

            foreach (var (uid, historical) in strike.Replaced)
                historical.AddIntersections(uid, ray, range, results);
            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // Mounted combat removes the attacker's own mount here. Invoke the
            // same event after merging, before choosing the nearest intersections.
            var sort = new RayCastSort(results);
            RaiseLocalEvent(user, ref sort);
            results = sort.HitEntities;
            if (results.Count == 0)
                continue;
            var nearest = results[0].Distance;
            foreach (var hit in results)
            {
                // Native ArcRayCast accepts every exact tie, not an arbitrary one.
                if (hit.Distance.Equals(nearest) && Validate(strike, hit.HitEntity, range + 0.1f))
                    targets.Add(hit.HitEntity);
            }
        }
        return true;
    }

    /// <summary>Use the same frozen historical pose for nearest-target ordering.</summary>
    public bool TryGetHistoricalPosition(EntityUid user, EntityUid target, out Vector2 position)
    {
        position = default;
        if (!_strikes.TryPeek(out var strike) || strike.Session == null || strike.User != user ||
            !strike.Poses.TryGetValue(target, out var pose) ||
            !pose.Coordinates.IsValid(EntityManager))
            return false;
        position = _transform.ToMapCoordinates(pose.Coordinates).Position;
        return true;
    }

    /// <summary>Called by final wide-hit validation inside the same strike scope.</summary>
    public bool TryValidate(EntityUid user, EntityUid target, float range, ICommonSession? session, out bool valid)
    {
        valid = false;
        if (!_strikes.TryPeek(out var strike) || strike.Session == null || strike.User != user ||
            session == null || strike.Session.UserId != session.UserId)
            return false;
        valid = Validate(strike, target, range);
        return true;
    }

    private bool Validate(Strike strike, EntityUid target, float range)
    {
        if (TerminatingOrDeleted(target) || !TryComp<TransformComponent>(target, out var xform) ||
            xform.MapID != strike.Map)
            return false;
        if (!strike.Poses.TryGetValue(target, out var pose))
        {
            // Untracked world objects retain current geometry. Remember their
            // first checked pose too, rather than repeatedly reading transforms.
            if (!TryFreezePose(xform.Coordinates, xform.LocalRotation, strike.Map, out pose, out _, out _))
                return false;
            strike.Poses[target] = pose;
        }
        if (!pose.Coordinates.IsValid(EntityManager))
            return false;
        return _interaction.InRangeUnobstructed(strike.User, target, pose.Coordinates, pose.Angle,
            range, predicate: entity => strike.Replaced.ContainsKey(entity), overlapCheck: false);
    }

    private readonly record struct Pose(EntityCoordinates Coordinates, Angle Angle);

    private sealed class Strike(EntityUid user, ICommonSession? session, MapId map)
    {
        public readonly EntityUid User = user;
        public readonly ICommonSession? Session = session;
        public readonly MapId Map = map;
        public readonly Dictionary<EntityUid, Pose> Poses = new();
        public readonly Dictionary<EntityUid, HistoricalBody> Replaced = new();
    }

    private sealed class StrikeScope(ChargedAttackLagCompensationSystem system, Strike strike) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            if (!system._strikes.TryPeek(out var current) || current != strike)
                throw new InvalidOperationException("Charged melee strike scopes must be disposed in reverse order.");
            system._strikes.Pop();
            _disposed = true;
        }
    }

    private sealed class HistoricalBody(List<Box2> bounds, Matrix3x2 matrix, Matrix3x2 inverse)
    {
        public void AddIntersections(EntityUid uid, CollisionRay ray, float range, List<RayCastResults> results)
        {
            var localRay = new CollisionRay(Vector2.Transform(ray.Position, inverse),
                Vector2.TransformNormal(ray.Direction, inverse), ray.CollisionMask);
            foreach (var bound in bounds)
            {
                if (localRay.Intersects(bound, out var distance, out var point) &&
                    float.IsFinite(distance) && distance <= range)
                    results.Add(new RayCastResults(distance, Vector2.Transform(point, matrix), uid));
            }
        }
    }
}
