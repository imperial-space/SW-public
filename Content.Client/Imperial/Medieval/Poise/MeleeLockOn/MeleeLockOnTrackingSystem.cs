using System.Numerics;
using Content.Client.Eye;
using Content.Client.Movement.Systems;
using Content.Client.Viewport;
using Content.Shared.Camera;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>Local presentation and bounded viewport reports; never decides damage or authoritative expiry.</summary>
public sealed class MeleeLockOnTrackingSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEyeManager _eyes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transforms = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MeleeLockOnSystem _lockOn = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private MeleeLockOnMarkerOverlay _markerOverlay = default!;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan MinimumReportInterval = TimeSpan.FromSeconds(0.1);

    private EntityUid? _user;
    private EntityUid? _target;
    private uint _generation;
    private MapId _mapId = MapId.Nullspace;
    private TimeSpan? _outsideSince;
    private TimeSpan? _localLockExpiresAt;
    private TimeSpan _lastReport;
    private bool? _lastReportedVisible;
    private Vector2 _lastReportedExtents;
    private readonly MeleeLockOnCameraState _camera = new();
    private MeleeLockOnVisibilityCache _visibility;
    private bool _drawMarker;
    private bool _blinkOff;
    private Color? _debugLineColor;

    public bool DebugEnabled { get; set; }

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(EyeSystem));
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        UpdatesBefore.Add(typeof(ContentEyeSystem));
        SubscribeLocalEvent<MeleeLockOnComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<MeleeLockOnComponent, MeleeLockOnChangedEvent>(OnLockChanged);
        _markerOverlay = new MeleeLockOnMarkerOverlay();
        _overlays.AddOverlay(_markerOverlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_markerOverlay);
        base.Shutdown();
    }

    private void OnLockChanged(EntityUid user, MeleeLockOnComponent comp, ref MeleeLockOnChangedEvent args)
    {
        if (user == _user && comp.Target == null)
            Reset();
    }

    private void OnEyeOffset(EntityUid user, MeleeLockOnComponent comp, ref GetEyeOffsetEvent args)
    {
        if (user == _players.LocalEntity && user == _user && comp.Target != null &&
            Transform(user).MapID == _mapId && _lockOn.CanFollowCamera(user) &&
            TryComp<EyeComponent>(user, out var eye))
        {
            var eyeRotation = eye.Eye.Rotation;
            var inverseEyeRotation = -eyeRotation;
            args.Offset += inverseEyeRotation.RotateVec(_camera.Offset);
        }
    }

    public bool ShouldDrawMarker(EntityUid user, EntityUid target)
        => _user == user && _target == target && _drawMarker && !_blinkOff;

    public Color? GetDebugLineColor(EntityUid user, EntityUid target)
        => DebugEnabled && _user == user && _target == target ? _debugLineColor : null;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_players.LocalEntity is not { } user ||
            !TryComp<MeleeLockOnComponent>(user, out var comp) || comp.Target is not { } target ||
            !TryComp<EyeComponent>(user, out var eye) || eye.Eye != _eyes.CurrentEye ||
            !TryGetViewport(out var viewport, out var screenBounds, out var halfExtents))
        {
            Reset();
            return;
        }

        var mapId = Transform(user).MapID;
        if (_user != user || _mapId != mapId)
        {
            Reset();
            _user = user;
            _mapId = mapId;
        }
        if (_target != target || _generation != comp.LockGeneration)
        {
            // The Player's lock and pan continue through a direct Target switch.
            // Only the new Target's visibility/report state needs restarting.
            _target = target;
            _generation = comp.LockGeneration;
            _outsideSince = null;
            _lastReportedVisible = null;
            _visibility.Reset();
        }

        var now = _timing.CurTime;
        // A replacement may arrive before its entity state. Hold the current pan
        // during the normal tolerance, still respecting viewport resizing/zoom.
        if (!TryComp(target, out TransformComponent? targetTransform) || TerminatingOrDeleted(target) ||
            targetTransform.MapID != mapId)
        {
            _outsideSince ??= now;
            _camera.Update(_camera.Offset, halfExtents, comp.MaxCameraPanFraction,
                !MeleeLockOnMath.Expired(now, _outsideSince, comp.CameraTargetLossTolerance),
                _lockOn.CanFollowCamera(user), frameTime);
            _drawMarker = false;
            _debugLineColor = null;
            return;
        }

        var targetPosition = _transforms.GetWorldPosition(target);
        var playerPosition = _transforms.GetWorldPosition(user);
        var delta = targetPosition - playerPosition;
        var fraction = MeleeLockOnMath.ViewFraction(delta, halfExtents, eye.Eye.Rotation);
        var visible = TargetVisible(user, target, targetTransform, viewport, screenBounds, now);
        _outsideSince = visible ? null : _outsideSince ?? now;
        var atRisk = MeleeLockOnMath.LockAtRisk(visible, fraction, comp.FirstDistanceThreshold);
        _localLockExpiresAt = atRisk
            ? _localLockExpiresAt ?? now + MeleeLockOnMath.ClampLockOnTime(comp.LockOnTimeRemaining, comp.LockOnTimeout)
            : null;

        // The camera reacts immediately; use the replicated deadline for the lock's
        // warning once it arrives. A local fallback covers the first round trip.
        var expiresAt = atRisk ? comp.LockOnExpiresAt ?? _localLockExpiresAt : null;
        var expired = expiresAt is { } deadline && now >= deadline;
        _drawMarker = !expired &&
                      (MetaData(target).Flags & MetaDataFlags.Detached) == 0;
        _blinkOff = MeleeLockOnMath.Warning(now, expiresAt, comp.LockOnExpiryWarningDuration) &&
                    // Two complete flashes per second, using game time rather than frames.
                    (int) (now.TotalSeconds * 4) % 2 != 0;

        var cameraEnabled = _lockOn.CanFollowCamera(user);
        var followingAllowed = _drawMarker && cameraEnabled &&
                               !MeleeLockOnMath.Expired(now, _outsideSince, comp.CameraTargetLossTolerance);
        var toleranceRunning = _outsideSince != null &&
                               !MeleeLockOnMath.Expired(now, _outsideSince, comp.CameraTargetLossTolerance);
        _debugLineColor = expired ? null : toleranceRunning ? Color.Red :
            expiresAt != null ? Color.Yellow : Color.Green;
        var normalOffset = MeleeLockOnMath.FollowOffset(delta, halfExtents, eye.Eye.Rotation, comp.CameraPanStartThreshold,
            comp.CameraPanMultiplier, comp.FirstDistanceThreshold + comp.CameraFollowRangeExtension);
        var eyeRotation = eye.Eye.Rotation;
        var localOffset = eyeRotation.RotateVec(normalOffset);
        _camera.Update(localOffset, halfExtents,
            comp.MaxCameraPanFraction, followingAllowed, cameraEnabled, frameTime);

        var changed = _lastReportedVisible != visible ||
                      Vector2.DistanceSquared(halfExtents, _lastReportedExtents) > 0.001f;
        if (_lastReportedVisible == null || now - _lastReport >= (changed ? MinimumReportInterval : ReportInterval))
        {
            RaiseNetworkEvent(new MeleeLockOnViewEvent(GetNetEntity(target), comp.LockGeneration, halfExtents,
                visible));
            _lastReport = now;
            _lastReportedVisible = visible;
            _lastReportedExtents = halfExtents;
        }
    }

    private bool TargetVisible(EntityUid user, EntityUid target, TransformComponent transform,
        ScalingViewport viewport, Box2 screenBounds, TimeSpan now)
    {
        if ((TryComp<BlindableComponent>(user, out var blind) && blind.IsBlind) ||
            (MetaData(target).Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0 ||
            !TryComp<SpriteComponent>(target, out var sprite) || !sprite.Visible)
            return false;

        // An enemy is completely off screen only when its entire sprite has left the viewport.
        if (!OnScreen(target, transform, viewport, screenBounds))
            return false;

        // Keep cheap visibility/sprite-edge tests per frame; only raycast when the
        // cached LOS result expires. This also avoids allocating the predicate per frame.
        if (_visibility.TryGet(now, out var unoccluded))
            return unoccluded;

        unoccluded = _examine.InRangeUnOccluded(user, target, 0, predicate: entity => entity == user || entity == target);
        _visibility.Store(now, unoccluded);
        return unoccluded;
    }

    private bool OnScreen(EntityUid entity, TransformComponent transform, ScalingViewport viewport, Box2 screenBounds)
    {
        var (position, rotation) = _transforms.GetWorldPositionRotation(transform);
        if (!TryComp<SpriteComponent>(entity, out var sprite) || !sprite.Visible)
            return screenBounds.Contains(viewport.WorldToScreen(position));

        var bounds = _sprites.CalculateBounds((entity, sprite), position, rotation, _eyes.CurrentEye.Rotation);
        return screenBounds.Intersects(viewport.GetWorldToScreenMatrix().TransformBox(bounds));
    }

    private bool TryGetViewport(out ScalingViewport viewport, out Box2 screenBounds, out Vector2 halfExtents)
    {
        viewport = default!;
        screenBounds = default;
        halfExtents = default;
        if (_eyes.MainViewport is not ScalingViewport vp || vp.Eye == null || vp.PixelSize.X <= 0 || vp.PixelSize.Y <= 0)
            return false;

        // Intersect the rendered game with the control: excludes black bars and clipped
        // content, and accounts for separated chat/HUD layouts and display scaling.
        var matrix = vp.GetLocalToScreenMatrix();
        var renderedMin = Vector2.Transform(Vector2.Zero, matrix);
        var renderedMax = Vector2.Transform((Vector2) (vp.ViewportSize * vp.CurrentRenderScale), matrix);
        var minimum = Vector2.Max(renderedMin, vp.GlobalPixelPosition);
        var maximum = Vector2.Min(renderedMax, vp.GlobalPixelPosition + (Vector2) vp.PixelSize);
        if (maximum.X <= minimum.X || maximum.Y <= minimum.Y)
            return false;

        screenBounds = new Box2(minimum, maximum);
        var center = (minimum + maximum) / 2f;
        var left = vp.ScreenToMap(new Vector2(minimum.X, center.Y)).Position;
        var right = vp.ScreenToMap(new Vector2(maximum.X, center.Y)).Position;
        var top = vp.ScreenToMap(new Vector2(center.X, minimum.Y)).Position;
        var bottom = vp.ScreenToMap(new Vector2(center.X, maximum.Y)).Position;
        halfExtents = new Vector2(Vector2.Distance(left, right), Vector2.Distance(top, bottom)) / 2f;
        viewport = vp;
        return MeleeLockOnMath.ValidHalfExtents(halfExtents);
    }

    private void Reset()
    {
        _user = null;
        _target = null;
        _mapId = MapId.Nullspace;
        _outsideSince = null;
        _localLockExpiresAt = null;
        _lastReportedVisible = null;
        _visibility.Reset();
        _camera.Reset();
        _drawMarker = false;
        _blinkOff = false;
        _debugLineColor = null;
    }
}

/// <summary>A local LOS sample lasting at most 50 ms, with no allocation or per-frame delegate.</summary>
internal struct MeleeLockOnVisibilityCache
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.05);
    private TimeSpan _checkedAt;
    private bool _hasSample;
    private bool _unoccluded;

    public bool TryGet(TimeSpan now, out bool unoccluded)
    {
        unoccluded = _unoccluded;
        return _hasSample && now >= _checkedAt && now - _checkedAt < CheckInterval;
    }

    public void Store(TimeSpan now, bool unoccluded)
    {
        _checkedAt = now;
        _hasSample = true;
        _unoccluded = unoccluded;
    }

    public void Reset() => this = default;
}
