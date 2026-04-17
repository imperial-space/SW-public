using Content.Shared.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.CapturePoint.Systems;
using Robust.Client.Graphics;
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

    public bool OverlayActive;
    public NetEntity OverlayPointEntity;

    private CapturePointOverlay? _captureOverlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CapturePointOverlayUpdateEvent>(OnOverlayUpdate);
        SubscribeNetworkEvent<CapturePointMessengerEvent>(OnMessenger);
        SubscribeNetworkEvent<CapturePointResultEvent>(OnResult);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_captureOverlay != null)
            _overlay.RemoveOverlay(_captureOverlay);
    }

    private void EnsureOverlay()
    {
        if (_captureOverlay != null)
            return;

        _captureOverlay = new CapturePointOverlay(this, _resourceCache, _entManager);
        _overlay.AddOverlay(_captureOverlay);
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

    private void OnOverlayUpdate(CapturePointOverlayUpdateEvent ev)
    {
        OverlayActive = ev.Active;
        OverlayPointEntity = ev.Point;

        if (ev.Active)
            EnsureOverlay();
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
