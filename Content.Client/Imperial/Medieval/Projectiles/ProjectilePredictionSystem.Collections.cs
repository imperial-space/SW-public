using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Client.Projectiles;

public sealed partial class ProjectilePredictionSystem
{
    // Each active snapshot gets its own buffer so nested callbacks cannot
    // overwrite the outer snapshot. Buffers belong to this system instance.
    private readonly Dictionary<Type, object> _snapshotBuffers = [];

    private ProjectileSnapshot<T> Snapshot<T>(ICollection<T> source)
    {
        var count = source.Count;
        if (count == 0)
            return default;

        if (!_snapshotBuffers.TryGetValue(typeof(T), out var cached))
        {
            cached = new List<ProjectileSnapshotBuffer<T>>();
            _snapshotBuffers.Add(typeof(T), cached);
        }
        var buffers = (List<ProjectileSnapshotBuffer<T>>)cached;
        ProjectileSnapshotBuffer<T>? buffer = null;
        foreach (var available in buffers)
        {
            if (available.InUse)
                continue;
            buffer = available;
            break;
        }
        if (buffer == null)
        {
            buffer = new ProjectileSnapshotBuffer<T>();
            buffers.Add(buffer);
        }

        if (buffer.Items.Length < count)
            buffer.Items = new T[count];
        buffer.InUse = true;
        try
        {
            source.CopyTo(buffer.Items, 0);
            return new ProjectileSnapshot<T>(buffer, count);
        }
        catch
        {
            buffer.Release();
            throw;
        }
    }

    private sealed class ProjectileSnapshotBuffer<T>
    {
        public T[] Items = [];
        public bool InUse;

        public void Release()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Array.Clear(Items);
            InUse = false;
        }
    }

    private readonly struct ProjectileSnapshot<T>(ProjectileSnapshotBuffer<T>? buffer, int count) : IDisposable
    {
        public Enumerator GetEnumerator() => new(buffer?.Items ?? [], count);

        public void Dispose()
        {
            buffer?.Release();
        }

        public struct Enumerator(T[] items, int count)
        {
            private int _index = -1;

            public readonly T Current => items[_index];

            public bool MoveNext()
            {
                return ++_index < count;
            }

        }
    }

    private static bool HasSingleHardFixture(FixturesComponent fixtures)
    {
        var found = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;
            if (found)
                return false;
            found = true;
        }
        return found;
    }

    private void OnShotCompleted(ProjectileShotCompletedEvent args)
    {
        if (args.Launched)
            return;
        using var localKeys = Snapshot(_local.Keys);
        foreach (var key in localKeys)
            if (key.Weapon == args.Weapon && key.InputTick == args.InputTick)
                Retire(key);
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent args)
    {
        if (_timing.IsFirstTimePredicted)
        {
            foreach (var (uid, flight) in _flight)
            {
                if (flight.Tick == _timing.CurTick || !TryComp(uid, out TransformComponent? xform))
                    continue;
                flight.Previous = _transform.GetMapCoordinates(uid, xform);
                flight.Tick = _timing.CurTick;
            }

            using var local = Snapshot(_local);
            foreach (var (key, uid) in local)
            {
                if (!TryComp<ProjectilePredictionComponent>(uid, out var predicted) || predicted.Impact ||
                    !TryComp<ProjectileComponent>(uid, out var projectile) || projectile.ProjectileSpent ||
                    !TryComp<PhysicsComponent>(uid, out var body) || !body.CanCollide ||
                    _fixtures.GetFixtureOrNull(uid, SharedProjectileSystem.ProjectileFixture) is not { } sensor ||
                    sensor.Contacts.Count == 0)
                    continue;
                _overlapping.Clear();
                _lookup.GetEntitiesIntersecting(Transform(uid).MapID, sensor.Shape,
                    _physics.GetPhysicsTransform(uid), _overlapping, LookupFlags.Dynamic | LookupFlags.Static);
                using var contacts = Snapshot(sensor.Contacts);
                foreach (var (otherFixture, contact) in contacts)
                {
                    var target = otherFixture.Owner;
                    if (contact.Deleting || !contact.Enabled || !otherFixture.Hard || !_overlapping.Contains(target) ||
                        target == predicted.Source || target == projectile.Shooter || target == projectile.Weapon ||
                        TryComp<ProjectilePredictionComponent>(target, out var matching) && matching.Key == key ||
                        !TryComp<PhysicsComponent>(target, out var targetBody) || !targetBody.CanCollide ||
                        !TryComp<FixturesComponent>(target, out var targetFixtures) ||
                        !HasSingleHardFixture(targetFixtures))
                        continue;
                    PresentContact((uid, predicted), projectile, target, body);
                    if (predicted.Impact)
                        break;
                }
            }
            return;
        }
        _replay.Clear();
        foreach (var uid in _local.Values)
            if (TryComp<PhysicsComponent>(uid, out var body) && TryComp(uid, out TransformComponent? xform))
                _replay[uid] = (xform.Coordinates, xform.LocalRotation, body.LinearVelocity, body.AngularVelocity);
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        using var local = Snapshot(_local);
        foreach (var (key, uid) in local)
        {
            if (!Enabled || !Exists(uid) || _player.LocalEntity == null ||
                !_server.ContainsKey(key) && _timing.RealTime - _created[key] > TimeSpan.FromSeconds(3))
                Retire(key);
            else if (_server.TryGetValue(key, out var server))
            {
                if (TerminatingOrDeleted(server) || !TryComp(server, out TransformComponent? realTransform) ||
                    realTransform.MapID == MapId.Nullspace ||
                    !TryComp<ProjectileComponent>(server, out var real) || real.ProjectileSpent ||
                    real.Shooter != _player.LocalEntity ||
                    TryComp<EmbeddableProjectileComponent>(server, out var embedded) && embedded.EmbeddedIntoUid != null)
                    Retire(key);
                else
                {
                    SetVisible(server, false);
                    // A real state past the predicted contact can establish a miss without a client hit report.
                    // This is an impact disagreement, not routine correction of an uninterrupted flight.
                    var localPrediction = Comp<ProjectilePredictionComponent>(uid);
                    if (localPrediction.Impact && !_confirmed.ContainsKey(key) &&
                        TryComp<ProjectilePredictionComponent>(server, out var realPrediction) &&
                        localPrediction.ContactTick >= localPrediction.LaunchTick)
                    {
                        var flightTicks = localPrediction.ContactTick.Value - localPrediction.LaunchTick.Value;
                        if (_timing.LastRealTick > realPrediction.LaunchTick + flightTicks + 2u)
                            Retire(key);
                    }
                }
            }
        }
        using var created = Snapshot(_created.Keys);
        foreach (var key in created)
            if (!_local.ContainsKey(key) && _timing.RealTime - _created[key] > TimeSpan.FromSeconds(10))
                _created.Remove(key);
        using var presented = Snapshot(_presented);
        foreach (var (key, value) in presented)
            if (_timing.RealTime - value.Time > TimeSpan.FromSeconds(10))
                _presented.Remove(key);
        using var confirmed = Snapshot(_confirmed);
        foreach (var (key, time) in confirmed)
            if (_timing.RealTime - time > TimeSpan.FromSeconds(10))
                _confirmed.Remove(key);
    }

    private void OnDetach(LocalPlayerDetachedEvent args)
    {
        using var localKeys = Snapshot(_local.Keys);
        foreach (var key in localKeys)
            Retire(key);
        _server.Clear();
        _serverKeys.Clear();
        _created.Clear();
        _presented.Clear();
        _confirmed.Clear();
        _replay.Clear();
        _flight.Clear();
        _snapshotBuffers.Clear();
    }
}
