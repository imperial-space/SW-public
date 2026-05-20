using System;
using System.Collections.Generic;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Shared.Damage;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

public sealed class PlayerDrowningSystem : EntitySystem
{
    private const float DefaultReloadTimeSeconds = 1f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        base.Initialize();
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

        SubscribeLocalEvent<PlayerDrowningComponent, ComponentInit>(OnDrowningInit);
        SubscribeLocalEvent<PlayerDrowningComponent, ComponentShutdown>(OnDrowningShutdown);
        SubscribeLocalEvent<PlayerDrowningComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<UndrowableComponent, ComponentInit>(OnUndrowableInit);
    }

    private void OnUndrowableInit(Entity<UndrowableComponent> ent, ref ComponentInit args)
    {
        ResetDrowning(ent);
    }

    private void OnDrowningInit(Entity<PlayerDrowningComponent> ent, ref ComponentInit args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnDrowningShutdown(Entity<PlayerDrowningComponent> ent, ref ComponentShutdown args)
    {
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

        var curTime = _timing.CurTime;
        if (curTime < _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);
        var seaMaps = CollectSeaMaps();
        var resetQueue = new List<EntityUid>();
        var processQueue = new List<EntityUid>();

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var transform))
        {
            var onGrid = transform.GridUid is { } gridUid && HasComp<MapGridComponent>(gridUid);
            var resetDrowning = !seaMaps.Contains(transform.MapID) ||
                                HasComp<MapComponent>(uid) ||
                                HasComp<MapGridComponent>(uid) ||
                                HasComp<WaveComponent>(uid) ||
                                IsAttachedToGhost(uid, transform) ||
                                HasComp<UndrowableComponent>(uid) ||
                                onGrid;

            if (resetDrowning)
                resetQueue.Add(uid);
            else
                processQueue.Add(uid);
        }

        foreach (var uid in resetQueue)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            ResetDrowning(uid);
        }

        foreach (var uid in processQueue)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            ProcessDrowning(uid);
        }
    }

    private HashSet<MapId> CollectSeaMaps()
    {
        var seaMaps = new HashSet<MapId>();

        var query = EntityQueryEnumerator<SeaComponent>();
        while (query.MoveNext(out var uid, out var sea))
        {
            if (sea.Disabled)
                continue;

            seaMaps.Add(_transform.GetMapId(uid));
        }

        return seaMaps;
    }

    private void ResetDrowning(EntityUid uid)
    {
        if (!TryComp<PlayerDrowningComponent>(uid, out _))
            return;

        RemComp<PlayerDrowningComponent>(uid);
    }

    private void ProcessDrowning(EntityUid uid)
    {
        if (HasComp<UndrowableComponent>(uid))
        {
            ResetDrowning(uid);
            return;
        }

        var drowner = EnsureComp<PlayerDrowningComponent>(uid);
        drowner.DrownTime += 1;

        _damageable.TryChangeDamage(uid, drowner.DrowningDamage, true, false);

        if (drowner.DrownTime < drowner.MaxDrownTime)
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Alive)
        {
            drowner.DrownTime = 0;
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
