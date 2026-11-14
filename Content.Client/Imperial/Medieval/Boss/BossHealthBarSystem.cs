using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossHealthBarSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _resources = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new BossHealthBarOverlay(EntityManager, _resources));
        _overlays.AddOverlay(new BossHealthBarNameOverlay(EntityManager, _resources));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BossHealthBarOverlay>();
        _overlays.RemoveOverlay<BossHealthBarNameOverlay>();
        base.Shutdown();
    }
}
