using System;
using System.Collections.Generic;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

public sealed class PlayerDrowningSystem : EntitySystem
{
    private const float DefaultReloadTimeSeconds = 1f;
    private const int DrownTimeMax = 15;
    private static readonly DamageSpecifier DrowningDamage = new()
    {
        DamageDict = new()
        {
            { "Asphyxiation", 10 }
        }
    };

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private readonly HashSet<MapId> _seaMaps = new();
    private readonly List<EntityUid> _resetQueue = new();
    private readonly List<EntityUid> _processQueue = new();
    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        base.Initialize();
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);
        CollectSeaMaps();
        _resetQueue.Clear();
        _processQueue.Clear();

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var transform))
        {
            var onGrid = transform.GridUid is { } gridUid && HasComp<MapGridComponent>(gridUid);
            var resetDrowning = !_seaMaps.Contains(transform.MapID) ||
                                HasComp<MapComponent>(uid) ||
                                HasComp<MapGridComponent>(uid) ||
                                HasComp<WaveComponent>(uid) ||
                                IsAttachedToGhost(uid, transform) ||
                                HasComp<UndrowableComponent>(uid) ||
                                onGrid;

            if (resetDrowning)
                _resetQueue.Add(uid);
            else
                _processQueue.Add(uid);
        }

        foreach (var uid in _resetQueue)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            ResetDrowning(uid);
        }

        foreach (var uid in _processQueue)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            ProcessDrowning(uid);
        }
    }

    private void CollectSeaMaps()
    {
        _seaMaps.Clear();

        var query = EntityQueryEnumerator<SeaComponent>();
        while (query.MoveNext(out var uid, out var sea))
        {
            if (sea.Disabled)
                continue;

            _seaMaps.Add(_transform.GetMapId(uid));
        }
    }

    private void ResetDrowning(EntityUid uid)
    {
        if (!TryComp<PlayerDrowningComponent>(uid, out _))
            return;

        RemComp<PlayerDrowningComponent>(uid);
    }

    private void ProcessDrowning(EntityUid uid)
    {
        var drowner = EnsureComp<PlayerDrowningComponent>(uid);
        drowner.DrownTime += 1;

        _damageable.TryChangeDamage(uid, DrowningDamage, true, false);

        if (drowner.DrownTime >= DrownTimeMax)
            QueueDel(uid);
    }

    private bool IsAttachedToGhost(EntityUid uid, TransformComponent transform)
    {
        if (HasComp<GhostComponent>(uid))
            return true;

        var parent = transform.ParentUid;
        var depth = 0;

        while (parent.IsValid() && depth < 32)
        {
            if (HasComp<GhostComponent>(parent))
                return true;

            if (!TryComp<TransformComponent>(parent, out var parentTransform))
                break;

            parent = parentTransform.ParentUid;
            depth++;
        }

        return false;
    }
}
