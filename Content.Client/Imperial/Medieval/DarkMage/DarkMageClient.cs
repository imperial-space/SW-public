using Content.Shared.Imperial.DarkMage.Components;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.DarkMage;

public sealed partial class DarkMageAddOverlay : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    private DarkMageOverlay _overlay = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkMageAddOverlayEvent>(OnComponentStartup);
        SubscribeLocalEvent<DarkMageRemoveOverlayEvent>(OnComponentShutdown);
    }
    private void OnComponentStartup(DarkMageAddOverlayEvent args)
    {
        _overlay = new(_gameTiming.CurTime);
        _overlayManager.AddOverlay(_overlay);
    }
    private void OnComponentShutdown(DarkMageRemoveOverlayEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }
}
