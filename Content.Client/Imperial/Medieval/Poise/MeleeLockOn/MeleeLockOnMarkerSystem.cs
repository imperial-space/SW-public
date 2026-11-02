using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>Local-only world overlay: other clients cannot see a player's selected target.</summary>
public sealed class MeleeLockOnMarkerSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    private MeleeLockOnMarkerOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new MeleeLockOnMarkerOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        base.Shutdown();
    }
}
