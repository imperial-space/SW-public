using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Medieval.BlackSpire;

public sealed partial class BlackSpireSystem
{
    private void SpawnDefender(EntityUid uid, BlackSpireComponent component, CancellationToken token)
    {
        var totalWeight = component.SkeletonPool.Values.Where(weight => weight > 0f).Sum();
        if (totalWeight <= 0f)
            return;

        var roll = _random.NextFloat() * totalWeight;
        foreach (var (prototype, weight) in component.SkeletonPool)
        {
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll > 0f)
                continue;

            TrySpawnSkeleton(uid, component, uid, prototype, component.SpawnMinRadius, component.SpawnMaxRadius, token);
            return;
        }
    }

    private void TrySpawnSkeleton(EntityUid uid, BlackSpireComponent component, EntityUid origin, EntProtoId prototype,
        float minRadius, float maxRadius, CancellationToken token)
    {
        var originCoords = _transform.GetMapCoordinates(origin);
        if (originCoords.MapId == MapId.Nullspace)
            return;

        for (var i = 0; i < component.SpawnPositionAttempts; i++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var radius = minRadius + _random.NextFloat() * (maxRadius - minRadius);
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var coordinates = new MapCoordinates(originCoords.Position + offset, originCoords.MapId);
            if (!CanSpawnAt(origin, coordinates, component.SpawnClearance))
                continue;

            _ = SpawnSkeletonAsync(uid, component, origin, prototype, coordinates, token);
            return;
        }
    }

    private bool CanSpawnAt(EntityUid origin, MapCoordinates coordinates, float clearance)
    {
        if (TerminatingOrDeleted(origin))
            return false;

        var start = _transform.GetMapCoordinates(origin);
        if (start.MapId != coordinates.MapId)
            return false;

        var direction = coordinates.Position - start.Position;
        if (direction.LengthSquared() <= 0f)
            return false;

        var ray = new CollisionRay(start.Position, Vector2.Normalize(direction), (int) CollisionGroup.MobMask);
        if (_physics.IntersectRay(coordinates.MapId, ray, direction.Length(), origin).Any())
            return false;

        var bounds = new Box2(coordinates.Position - new Vector2(clearance), coordinates.Position + new Vector2(clearance));
        foreach (var nearby in _lookup.GetEntitiesIntersecting(coordinates.MapId, bounds, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (nearby == origin || !TryComp<PhysicsComponent>(nearby, out var physics) || !physics.CanCollide ||
                !TryComp<FixturesComponent>(nearby, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard && (fixture.CollisionLayer & (int) CollisionGroup.MobMask) != 0)
                    return false;
            }
        }

        return true;
    }

    private async Task SpawnSkeletonAsync(EntityUid uid, BlackSpireComponent component, EntityUid origin,
        EntProtoId prototype, MapCoordinates coordinates, CancellationToken token)
    {
        var effect = Spawn(component.SpawnEffect, _transform.ToCoordinates(coordinates));
        _audio.PlayPvs(component.SpawnSound, effect, AudioParams.Default.WithVariation(0.15f));

        try
        {
            await Timer.Delay(component.SpawnDelay, token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (IsActive(uid, component) && CanSpawnAt(origin, coordinates, component.SpawnClearance))
                Spawn(prototype, _transform.ToCoordinates(coordinates));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            if (!TerminatingOrDeleted(effect))
                QueueDel(effect);
        }
    }
}
