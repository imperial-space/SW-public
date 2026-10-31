using Content.Client.Eye;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossArenaSpectatorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BossArenaSpectatorWindow? _window;
    private EntityUid? _camera;
    private NetEntity? _cameraNet;
    private bool _eyeTracked;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BossArenaSpectatorWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not BossArenaSpectatorUiState spectator) return;
        if (_cameraNet != spectator.Camera)
        {
            if (_camera is { } old) EntMan.System<EyeLerpingSystem>().RemoveEye(old);
            _cameraNet = spectator.Camera;
            _camera = null;
            _eyeTracked = false;
        }
        BindEye();
    }

    public override void Update()
    {
        base.Update();
        BindEye();
    }

    private void BindEye()
    {
        if (_window == null || _cameraNet is not { } network ||
            !EntMan.TryGetEntity(network, out var resolved) || resolved is not { } camera ||
            !EntMan.TryGetComponent<EyeComponent>(camera, out var eye)) return;
        _camera = camera;
        if (!_eyeTracked)
        {
            EntMan.System<EyeLerpingSystem>().AddEye(camera, eye);
            _eyeTracked = true;
        }
        _window.SetEye(eye.Eye);
    }

    protected override void Dispose(bool disposing)
    {
        if (_camera is { } camera) EntMan.System<EyeLerpingSystem>().RemoveEye(camera);
        _camera = null;
        _cameraNet = null;
        _eyeTracked = false;
        if (disposing) _window?.Dispose();
        _window = null;
        base.Dispose(disposing);
    }
}
