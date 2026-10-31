using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossCastBarSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new BossCastBarOverlay(EntityManager, _resources, _timing));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BossCastBarOverlay>();
        base.Shutdown();
    }
}
