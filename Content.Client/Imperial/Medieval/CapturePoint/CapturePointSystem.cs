using System.Numerics;
using Content.Shared.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.CapturePoint.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.CapturePoint;

public sealed class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public NetEntity OverlayPointEntity;

    private CapturePointOverlay? _captureOverlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CapturePointMessengerEvent>(OnMessenger);
        SubscribeNetworkEvent<CapturePointResultEvent>(OnResult);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_captureOverlay != null)
            _overlay.RemoveOverlay(_captureOverlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateZoneCheck();
    }

    private void UpdateZoneCheck()
    {
        var player = _playerManager.LocalEntity;
        if (player == null)
        {
            SetOverlayState(null);
            return;
        }

        var playerPos = _transform.GetMapCoordinates(player.Value);
        EntityUid? foundPoint = null;

        var query = EntityQueryEnumerator<CapturePointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != playerPos.MapId)
                continue;

            var (pointPos, pointRot) = _transform.GetWorldPositionRotation(uid);
            var half = comp.Radius;
            var box = Box2.CenteredAround(pointPos, new Vector2(half * 2f, half * 2f));
            var rotated = new Box2Rotated(box, pointRot, pointPos);

            if (!rotated.Contains(playerPos.Position))
                continue;

            foundPoint = uid;
            break;
        }

        SetOverlayState(foundPoint);
    }

    private void SetOverlayState(EntityUid? point)
    {
        var hasOverlay = _overlay.HasOverlay<CapturePointOverlay>();

        if (point is not { } uid)
        {
            if (hasOverlay)
            {
                _overlay.RemoveOverlay<CapturePointOverlay>();
                _captureOverlay = null;
                OverlayPointEntity = default;
            }

            return;
        }

        if (!TryGetNetEntity(uid, out var newNet))
            return;

        if (OverlayPointEntity == newNet.Value)
            return;

        OverlayPointEntity = newNet.Value;

        if (!hasOverlay)
        {
            _captureOverlay = new CapturePointOverlay(this, _resourceCache, _entManager);
            _overlay.AddOverlay(_captureOverlay);
        }
    }

    public float GetCaptureProgress()
    {
        if (!TryComp<CapturePointComponent>(GetEntity(OverlayPointEntity), out var comp))
            return 0f;

        if (comp.State != CapturePointState.Capturing || comp.CurrentCaptureDuration <= 0f)
            return 0f;

        var elapsed = (float)(_timing.CurTime - comp.CaptureStartTime).TotalSeconds;
        return Math.Clamp(elapsed / comp.CurrentCaptureDuration, 0f, 1f);
    }

    public float GetCaptureTimeRemaining()
    {
        if (!TryComp<CapturePointComponent>(GetEntity(OverlayPointEntity), out var comp))
            return 0f;

        if (comp.State != CapturePointState.Capturing)
            return 0f;

        var elapsed = (float)(_timing.CurTime - comp.CaptureStartTime).TotalSeconds;
        return Math.Max(0f, comp.CurrentCaptureDuration - elapsed);
    }

    public float GetCooldownRemaining()
    {
        if (!TryComp<CapturePointComponent>(GetEntity(OverlayPointEntity), out var comp))
            return 0f;

        if (comp.State != CapturePointState.Cooldown)
            return 0f;

        var elapsed = (float)(_timing.CurTime - comp.CooldownStartTime).TotalSeconds;
        return Math.Max(0f, comp.CooldownDuration - elapsed);
    }

    private void OnMessenger(CapturePointMessengerEvent ev)
    {
        var controller = _uiManager.GetUIController<CapturePointUIController>();
        controller.OpenMessengerWindow(ev);
    }

    private void OnResult(CapturePointResultEvent ev)
    {
        var controller = _uiManager.GetUIController<CapturePointUIController>();
        controller.ShowResultPopup(ev);
    }
}
