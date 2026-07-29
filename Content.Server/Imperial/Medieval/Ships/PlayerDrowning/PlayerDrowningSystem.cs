using System;
using System.Collections.Generic;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Drowning;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

public sealed class PlayerDrowningSystem : EntitySystem
{
    private const float DrowningUpdateInterval = 1f;
    private const float SpawnShieldDuration = 45f;
    private const int CandidateChecksPerUpdate = 128;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private readonly HashSet<MapId> _seaMaps = new();
    private readonly Queue<EntityUid> _candidateQueue = new();
    private readonly HashSet<EntityUid> _queuedCandidates = new();
    private readonly Queue<EntityUid> _activeQueue = new();
    private readonly HashSet<EntityUid> _activeDrowners = new();
    private readonly HashSet<EntityUid> _queuedDrowners = new();
    private readonly PriorityQueue<EntityUid, TimeSpan> _delayedCandidates = new();
    private readonly Dictionary<EntityUid, TimeSpan> _delayedCandidateTimes = new();
    private readonly Stack<EntityUid> _descendants = new();
    private float _activeCheckBudget;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeaComponent, ComponentStartup>(OnSeaStartup);
        SubscribeLocalEvent<SeaComponent, ComponentShutdown>(OnSeaShutdown);

        SubscribeLocalEvent<PhysicsComponent, MapInitEvent>(OnPhysicsMapInit);
        SubscribeLocalEvent<PhysicsComponent, EntParentChangedMessage>(OnPhysicsParentChanged);
        SubscribeLocalEvent<MobStateComponent, MoveEvent>(OnMobMoved);

        SubscribeLocalEvent<PlayerDrowningComponent, ComponentInit>(OnDrowningInit);
        SubscribeLocalEvent<PlayerDrowningComponent, ComponentShutdown>(OnDrowningShutdown);
        SubscribeLocalEvent<PlayerDrowningComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);

        SubscribeLocalEvent<UndrowableComponent, ComponentInit>(OnProtectionAdded);
        SubscribeLocalEvent<UndrowableComponent, ComponentShutdown>(OnProtectionRemoved);
        SubscribeLocalEvent<GodmodeComponent, ComponentInit>(OnParentProtectionAdded);
        SubscribeLocalEvent<GodmodeComponent, ComponentShutdown>(OnParentProtectionRemoved);
        SubscribeLocalEvent<ShieldOnStartupComponent, ComponentShutdown>(OnParentProtectionRemoved);
    }

    private void OnSeaStartup(Entity<SeaComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Disabled)
            return;

        var mapId = Transform(ent).MapID;
        if (mapId == MapId.Nullspace || !_seaMaps.Add(mapId))
            return;

        var query = EntityQueryEnumerator<PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID == mapId)
                QueueCandidate(uid, xform);
        }
    }

    private void OnSeaShutdown(Entity<SeaComponent> ent, ref ComponentShutdown args)
    {
        _seaMaps.Remove(Transform(ent).MapID);

        foreach (var uid in _activeDrowners)
            EnqueueCandidate(uid);
    }

    private void OnPhysicsMapInit(Entity<PhysicsComponent> ent, ref MapInitEvent args)
    {
        QueueCandidate(ent.Owner);
    }

    private void OnPhysicsParentChanged(Entity<PhysicsComponent> ent, ref EntParentChangedMessage args)
    {
        QueueCandidate(ent.Owner, args.Transform);
    }

    private void OnMobMoved(Entity<MobStateComponent> ent, ref MoveEvent args)
    {
        if (args.ParentChanged || !_seaMaps.Contains(args.Component.MapID))
            return;

        if (args.Component.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            args.OldPosition.EntityId != gridUid ||
            args.NewPosition.EntityId != gridUid)
        {
            return;
        }

        var oldPosition = args.OldPosition.Position;
        var newPosition = args.NewPosition.Position;
        var tileSize = grid.TileSize;
        if ((int) MathF.Floor(oldPosition.X / tileSize) != (int) MathF.Floor(newPosition.X / tileSize) ||
            (int) MathF.Floor(oldPosition.Y / tileSize) != (int) MathF.Floor(newPosition.Y / tileSize))
        {
            QueueCandidate(ent.Owner, args.Component);
        }
    }

    private void OnProtectionAdded(Entity<UndrowableComponent> ent, ref ComponentInit args)
    {
        EnqueueCandidate(ent.Owner);
    }

    private void OnProtectionRemoved(Entity<UndrowableComponent> ent, ref ComponentShutdown args)
    {
        EnqueueCandidate(ent.Owner);
    }

    private void OnParentProtectionAdded<T>(Entity<T> ent, ref ComponentInit args) where T : IComponent
    {
        QueueEntityAndDescendants(ent.Owner);
    }

    private void OnParentProtectionRemoved<T>(Entity<T> ent, ref ComponentShutdown args) where T : IComponent
    {
        QueueEntityAndDescendants(ent.Owner);
    }

    private void OnDrowningInit(Entity<PlayerDrowningComponent> ent, ref ComponentInit args)
    {
        UpdateDrowningModifier(ent);
        _activeDrowners.Add(ent.Owner);
        EnqueueDrowner(ent.Owner);
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnDrowningShutdown(Entity<PlayerDrowningComponent> ent, ref ComponentShutdown args)
    {
        _activeDrowners.Remove(ent.Owner);
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshMovementSpeed(Entity<PlayerDrowningComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!HasComp<MobStateComponent>(ent) || HasComp<GhostComponent>(ent) || HasComp<UndrowableComponent>(ent))
            return;

        args.ModifySpeed(ent.Comp.SpeedModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessDelayedCandidates();
        ProcessActiveDrowners(frameTime);
        ProcessCandidates();
    }

    private void ProcessDelayedCandidates()
    {
        var curTime = _timing.CurTime;
        while (_delayedCandidates.TryPeek(out var uid, out var retryAt) && retryAt <= curTime)
        {
            _delayedCandidates.Dequeue();
            if (!_delayedCandidateTimes.TryGetValue(uid, out var scheduledAt) || scheduledAt != retryAt)
                continue;

            _delayedCandidateTimes.Remove(uid);
            EnqueueCandidate(uid);
        }
    }

    private void ProcessActiveDrowners(float frameTime)
    {
        if (_activeDrowners.Count == 0)
        {
            _activeCheckBudget = 0f;
            return;
        }

        _activeCheckBudget = Math.Min(
            _activeCheckBudget + frameTime * _activeDrowners.Count / DrowningUpdateInterval,
            _activeDrowners.Count);
        var checks = Math.Min((int) _activeCheckBudget, _activeDrowners.Count);
        if (checks == 0)
            return;

        _activeCheckBudget -= checks;
        var processed = 0;
        while (processed < checks && _activeQueue.TryDequeue(out var uid))
        {
            _queuedDrowners.Remove(uid);
            if (!_activeDrowners.Contains(uid))
                continue;

            processed++;
            UpdateDrowner(uid);

            if (_activeDrowners.Contains(uid))
                EnqueueDrowner(uid);
        }
    }

    private void ProcessCandidates()
    {
        var checks = Math.Min(CandidateChecksPerUpdate, _candidateQueue.Count);
        for (var i = 0; i < checks; i++)
        {
            var uid = _candidateQueue.Dequeue();
            _queuedCandidates.Remove(uid);
            EvaluateCandidate(uid);
        }
    }

    private void EvaluateCandidate(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (Paused(uid))
        {
            ScheduleCandidate(uid, _timing.CurTime + TimeSpan.FromSeconds(DrowningUpdateInterval));
            return;
        }

        TimeSpan? retryAt = null;
        if (!TryComp(uid, out TransformComponent? xform) ||
            !_seaMaps.Contains(xform.MapID) ||
            !ShouldDrown(uid, xform, out retryAt))
        {
            if (retryAt is { } retry)
                ScheduleCandidate(uid, retry);

            ResetDrowning(uid);
            return;
        }

        if (TryComp<PlayerDrowningComponent>(uid, out _))
            return;

        var drowner = EnsureComp<PlayerDrowningComponent>(uid);
        ProcessDrowning(uid, drowner);
    }

    private void UpdateDrowner(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (Paused(uid))
            return;

        TimeSpan? retryAt = null;
        if (!TryComp<PlayerDrowningComponent>(uid, out var drowner) ||
            !TryComp(uid, out TransformComponent? xform) ||
            !_seaMaps.Contains(xform.MapID) ||
            !ShouldDrown(uid, xform, out retryAt))
        {
            if (retryAt is { } retry)
                ScheduleCandidate(uid, retry);

            ResetDrowning(uid);
            return;
        }

        ProcessDrowning(uid, drowner);
    }

    private bool ShouldDrown(EntityUid uid, TransformComponent xform, out TimeSpan? retryAt)
    {
        retryAt = null;
        return HasComp<PhysicsComponent>(uid)
               && !xform.Anchored
               && !HasComp<MapComponent>(uid)
               && !HasComp<MapGridComponent>(uid)
               && !HasComp<WaveComponent>(uid)
               && !HasComp<UndrowableComponent>(uid)
               && !_container.IsEntityOrParentInContainer(uid)
               && !IsProtectedOrGhost(uid, xform, out retryAt)
               && !IsOnSolidTile(xform);
    }

    private void QueueCandidate(EntityUid uid, TransformComponent? xform = null)
    {
        if (TerminatingOrDeleted(uid) || !Resolve(uid, ref xform, false))
            return;

        if (!HasComp<PlayerDrowningComponent>(uid))
        {
            if (!_seaMaps.Contains(xform.MapID) ||
                xform.Anchored ||
                HasComp<MapComponent>(uid) ||
                HasComp<MapGridComponent>(uid) ||
                HasComp<WaveComponent>(uid))
            {
                return;
            }
        }

        EnqueueCandidate(uid);
    }

    private void EnqueueCandidate(EntityUid uid)
    {
        if (_queuedCandidates.Add(uid))
            _candidateQueue.Enqueue(uid);
    }

    private void EnqueueDrowner(EntityUid uid)
    {
        if (_queuedDrowners.Add(uid))
            _activeQueue.Enqueue(uid);
    }

    private void QueueEntityAndDescendants(EntityUid root)
    {
        _descendants.Clear();
        _descendants.Push(root);

        while (_descendants.TryPop(out var uid))
        {
            QueueCandidate(uid);
            if (!TryComp(uid, out TransformComponent? xform))
                continue;

            var children = xform.ChildEnumerator;
            while (children.MoveNext(out var child))
                _descendants.Push(child);
        }
    }

    private void ScheduleCandidate(EntityUid uid, TimeSpan retryAt)
    {
        retryAt += _timing.TickPeriod;
        if (_delayedCandidateTimes.TryGetValue(uid, out var scheduledAt) && scheduledAt <= retryAt)
            return;

        _delayedCandidateTimes[uid] = retryAt;
        _delayedCandidates.Enqueue(uid, retryAt);
    }

    private void ResetDrowning(EntityUid uid)
    {
        if (TryComp<PlayerDrowningComponent>(uid, out _))
            RemComp<PlayerDrowningComponent>(uid);
    }

    private void UpdateDrowningModifier(Entity<PlayerDrowningComponent> ent)
    {
        var resistance = TryComp<DrowningModifierComponent>(ent, out var modifier)
            ? Math.Max(0.001f, modifier.ResistanceModifier)
            : 1f;
        if (Math.Abs(ent.Comp.AppliedResistanceModifier - resistance) < 0.0001f)
            return;

        var ratio = resistance / Math.Max(0.001f, ent.Comp.AppliedResistanceModifier);
        ent.Comp.AppliedResistanceModifier = resistance;
        ent.Comp.MaxDrownTime *= ratio;
        ent.Comp.DamageDrownDelay *= ratio;
        ent.Comp.DrowningDamage *= 1f / ratio;
    }

    private void ProcessDrowning(EntityUid uid, PlayerDrowningComponent drowner)
    {
        UpdateDrowningModifier((uid, drowner));
        drowner.DrownTime += DrowningUpdateInterval;

        if (drowner.DrownTime >= drowner.DamageDrownDelay)
            _damageable.TryChangeDamage(uid, drowner.DrowningDamage, true, false);

        if (drowner.DrownTime < drowner.MaxDrownTime)
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Dead)
        {
            drowner.DrownTime = 0;
            drowner.DamageDrownDelay = 0;
            return;
        }

        SinkEntity(uid, drowner);
    }

    private void SinkEntity(EntityUid uid, PlayerDrowningComponent component)
    {
        var mapCoordinates = _transform.GetMapCoordinates(uid);
        var soundCoordinates = new EntityCoordinates(_map.GetMapOrInvalid(mapCoordinates.MapId), mapCoordinates.Position);
        QueueDel(uid);
        Spawn(component.SplashEffect, mapCoordinates);
        _audio.PlayPvs(_random.Pick(MedievalShipSounds.Drown), soundCoordinates);
    }

    private bool IsProtectedOrGhost(EntityUid uid, TransformComponent xform, out TimeSpan? retryAt)
    {
        retryAt = null;
        var current = uid;
        var currentXform = xform;

        while (true)
        {
            if (HasComp<GhostComponent>(current) || HasComp<GodmodeComponent>(current))
                return true;

            if (TryComp<ShieldOnStartupComponent>(current, out var shield) && shield.Enabled)
            {
                var shieldExpires = shield.Spawned + TimeSpan.FromSeconds(SpawnShieldDuration);
                if (shieldExpires >= _timing.CurTime)
                {
                    retryAt = shieldExpires;
                    return true;
                }
            }

            var parent = currentXform.ParentUid;
            if (!parent.IsValid() || HasComp<MapComponent>(parent))
                return false;

            if (!TryComp(parent, out TransformComponent? parentXform))
                return false;

            currentXform = parentXform;
            current = parent;
        }
    }

    private bool IsOnSolidTile(TransformComponent transform)
    {
        if (transform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        var tileIndices = _map.CoordinatesToTile(gridUid, gridComp, transform.Coordinates);
        return _map.TryGetTileRef(gridUid, gridComp, tileIndices, out var tile) && !tile.Tile.IsEmpty;
    }
}
