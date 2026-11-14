using System.Collections.Generic;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss;

public abstract class BossOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;
    [Dependency] protected readonly IResourceCache Resources = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPlayerManager Player = default!;
    private readonly List<Overlay> _owned = new();

    protected abstract IEnumerable<Overlay> CreateOverlays();

    public override void Initialize()
    {
        base.Initialize();
        foreach (var overlay in CreateOverlays())
            if (_overlays.AddOverlay(overlay)) _owned.Add(overlay);
    }

    public override void Shutdown()
    {
        foreach (var overlay in _owned) _overlays.RemoveOverlay(overlay);
        _owned.Clear();
        base.Shutdown();
    }
}
