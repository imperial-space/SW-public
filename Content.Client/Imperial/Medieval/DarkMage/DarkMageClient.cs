using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.DarkMage;

public sealed partial class DarkMageAddOverlay : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private DarkMageOverlay _overlay = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkMageAddOverlayComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<DarkMageAddOverlayComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnComponentStartup(EntityUid uid, DarkMageAddOverlayComponent comp, ComponentStartup args)
    {
        if (_playerManager.LocalEntity != uid) return;
        _overlay = new(_gameTiming.CurTime);
        _overlayManager.AddOverlay(_overlay);
    }
    private void OnComponentShutdown(EntityUid uid, DarkMageAddOverlayComponent comp, ComponentShutdown args)
    {
        if (_playerManager.LocalEntity != uid) return;
        _overlayManager.RemoveOverlay(_overlay);
    }
}
