using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.Server.Imperial.Medieval.Prediction;

/// <summary>
/// Conservative broadphase for the retained movement history. Bounds live in
/// each sample's coordinate parent, just like the history itself, so moving or
/// rotating a grid does not invalidate them. Exact poses are still read by the
/// strike, using that attacker's reported snapshot.
/// </summary>
public sealed class MedievalMeleeHistoryIndex
{
    private readonly Dictionary<EntityUid, Body> _bodies = new();
    private readonly Dictionary<EntityUid, DynamicTree<Entry>> _parents = new();

    public Dictionary<EntityUid, DynamicTree<Entry>>.KeyCollection Parents => _parents.Keys;

    /// <summary>
    /// Called while the existing history queue is maintained, and immediately
    /// after a movement is recorded. Expanding an envelope is incremental;
    /// shrinking it only revisits the queue once per retention window.
    /// </summary>
    public void Refresh(EntityUid uid,
        Queue<(TimeSpan Time, EntityCoordinates Coordinates, Angle Rotation)> history,
        EntityCoordinates current, float radius, TimeSpan now, TimeSpan retention)
    {
        if (!_bodies.TryGetValue(uid, out var body))
        {
            body = new Body();
            _bodies.Add(uid, body);
            Rebuild(uid, body, history, current, radius, now, retention);
            return;
        }

        if (!radius.Equals(body.Radius) || body.NeedsShrink && now >= body.RebuildAt)
        {
            Rebuild(uid, body, history, current, radius, now, retention);
            return;
        }

        Include(uid, body, current);
    }

    private void Rebuild(EntityUid uid, Body body,
        Queue<(TimeSpan Time, EntityCoordinates Coordinates, Angle Rotation)> history,
        EntityCoordinates current, float radius, TimeSpan now, TimeSpan retention)
    {
        RemoveEntries(body);
        body.Radius = radius;
        body.RebuildAt = now + retention;
        body.NeedsShrink = false;
        foreach (var sample in history)
            Include(uid, body, sample.Coordinates);
        Include(uid, body, current);
    }

    private void Include(EntityUid uid, Body body, EntityCoordinates coordinates)
    {
        if (!coordinates.EntityId.IsValid() || !float.IsFinite(coordinates.X) || !float.IsFinite(coordinates.Y))
            return;

        var extent = new Vector2(body.Radius);
        var bounds = new Box2(coordinates.Position - extent, coordinates.Position + extent);
        if (body.Entries.TryGetValue(coordinates.EntityId, out var entry))
        {
            if (entry.Bounds.Contains(bounds))
                return;
            entry.Bounds = Box2.Union(entry.Bounds, bounds);
            _parents[coordinates.EntityId].Update(entry);
            body.NeedsShrink = true;
            return;
        }

        entry = new Entry(uid, bounds);
        body.Entries.Add(coordinates.EntityId, entry);
        if (!_parents.TryGetValue(coordinates.EntityId, out var tree))
        {
            tree = new DynamicTree<Entry>(static (in Entry value) => value.Bounds);
            _parents.Add(coordinates.EntityId, tree);
        }
        tree.Add(entry);
        body.NeedsShrink |= body.Entries.Count > 1;
    }

    public void Query(EntityUid parent, Box2 localBounds, HashSet<EntityUid> candidates)
    {
        if (!_parents.TryGetValue(parent, out var tree))
            return;
        tree.QueryAabb(ref candidates, static (ref HashSet<EntityUid> result, in Entry entry) =>
        {
            result.Add(entry.Uid);
            return true;
        }, localBounds);
    }

    public void Remove(EntityUid uid)
    {
        if (_bodies.Remove(uid, out var body))
            RemoveEntries(body);
    }

    private void RemoveEntries(Body body)
    {
        foreach (var (parent, entry) in body.Entries)
        {
            var tree = _parents[parent];
            tree.Remove(entry);
            if (tree.Count == 0)
                _parents.Remove(parent);
        }
        body.Entries.Clear();
    }

    public void Clear()
    {
        _bodies.Clear();
        _parents.Clear();
    }

    private sealed class Body
    {
        public readonly Dictionary<EntityUid, Entry> Entries = new();
        public float Radius;
        public TimeSpan RebuildAt;
        public bool NeedsShrink;
    }

    public sealed class Entry(EntityUid uid, Box2 bounds)
    {
        public readonly EntityUid Uid = uid;
        public Box2 Bounds = bounds;
    }
}
