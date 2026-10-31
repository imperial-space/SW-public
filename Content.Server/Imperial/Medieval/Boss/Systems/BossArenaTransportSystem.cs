using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaTransportSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public bool TryReturnPosition(EntityUid player, Entity<BossArenaMapComponent> arena, out EntityCoordinates destination, bool fallbackOwner = false)
    {
        destination = default;
        var valid = false;
        if (Exists(arena.Comp.Entrance) && TryComp<BossArenaEntranceComponent>(arena.Comp.Entrance, out var entrance))
        {
            destination = new EntityCoordinates(arena.Comp.Entrance, entrance.ArrivalOffset);
            valid = true;
        }
        var request = new BossArenaReturnEvent(player, arena, destination, valid) { AllowFallbackOwner = fallbackOwner };
        RaiseLocalEvent(ref request);
        destination = request.Destination;
        return request.Valid;
    }

    public void TransportUser(EntityUid user, EntityCoordinates target)
    {
        StopMotion(user);                                                        // clear movement before changing position

        _containers.TryRemoveFromContainer(user, force: true);
        _transform.SetCoordinates(user, target);
        _physics.SetLinearVelocity(user, Vector2.Zero);
        _physics.SetAngularVelocity(user, 0);
        var moved = new BossArenaPlayerEvent(user, BossArenaPlayerChange.Transported);
        RaiseLocalEvent(ref moved);
    }

    public void StopMotion(EntityUid user)
    {

        if (TryComp<PullableComponent>(user, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(user, pullable);
        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling is { } pulling &&
            TryComp<PullableComponent>(pulling, out var pulled))
            _pulling.TryStopPull(pulling, pulled);
        _physics.SetLinearVelocity(user, Vector2.Zero);
        _physics.SetAngularVelocity(user, 0);
    }

    public bool TryGetPlatformRecoveryPosition(EntityUid user, EntityUid grid, out EntityCoordinates target)
    {
        target = default;
        if (!Exists(grid) || Terminating(grid)) return false;
        var halfSize = Transform(grid).MapUid is { } map && TryComp<BossArenaMapComponent>(map, out var arena)
            ? arena.HalfSize : 5;
        foreach (var tile in Content.Shared.Imperial.Medieval.Boss.BossTilePatterns.GetPlatform(halfSize)
                     .OrderBy(tile => Math.Abs(tile.X) + Math.Abs(tile.Y + halfSize - 2)))
        {
            var point = _transform.ToMapCoordinates(new EntityCoordinates(grid, tile.X + 0.5f, tile.Y + 0.5f));
            if (TryGetClearPosition(user, grid, point, out target)) return true;
        }
        return false;
    }

    public bool TryGetClearPosition(EntityUid user, EntityUid parent, MapCoordinates mapTarget, out EntityCoordinates target)
    {
        target = _transform.ToCoordinates(parent, mapTarget);
        var mask = TryComp<PhysicsComponent>(user, out var body) ? body.CollisionMask : (int)CollisionGroup.MobMask;
        var bounds = Box2.CenteredAround(mapTarget.Position, new Vector2(0.7f));
        foreach (var obstacle in _lookup.GetEntitiesIntersecting(mapTarget.MapId, bounds, LookupFlags.Uncontained))
        {
            if (obstacle == user || !TryComp<PhysicsComponent>(obstacle, out var obstacleBody) || !obstacleBody.CanCollide ||
                !TryComp<FixturesComponent>(obstacle, out var fixtures))
                continue;
            if (HasComp<MobStateComponent>(obstacle))
                return false;
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard && (fixture.CollisionLayer & mask) != 0)
                    return false;
            }
        }
        return true;
    }
}
