using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Client.Player;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossTelegraphOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    public override void Initialize()
    {
        _overlays.AddOverlay(new BossFloorTelegraphOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossActorMarkerOverlay(EntityManager, _prototypes, _resources, _timing, _player));
        _overlays.AddOverlay(new BossExecutionLineOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossExecutionEyesOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossAutoProjectileOverlay(EntityManager, _resources, _prototypes, _timing));
        _overlays.AddOverlay(new BossCleaveAnimationOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossCircularSweepOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossRainCrystalOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossRiverOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossConstellationOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossSpreadSoulFloorOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossSpreadSoulMarkerOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossCommunionFogOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossCommunionLanternOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossVigilMarkOverlay(EntityManager, _prototypes, _resources, _timing));
        _overlays.AddOverlay(new BossVigilAshOverlay(EntityManager, _prototypes, _resources, _timing));
    }
    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BossFloorTelegraphOverlay>();
        _overlays.RemoveOverlay<BossActorMarkerOverlay>();
        _overlays.RemoveOverlay<BossExecutionLineOverlay>();
        _overlays.RemoveOverlay<BossExecutionEyesOverlay>();
        _overlays.RemoveOverlay<BossAutoProjectileOverlay>();
        _overlays.RemoveOverlay<BossCleaveAnimationOverlay>();
        _overlays.RemoveOverlay<BossCircularSweepOverlay>();
        _overlays.RemoveOverlay<BossRainCrystalOverlay>();
        _overlays.RemoveOverlay<BossRiverOverlay>();
        _overlays.RemoveOverlay<BossConstellationOverlay>();
        _overlays.RemoveOverlay<BossSpreadSoulFloorOverlay>();
        _overlays.RemoveOverlay<BossSpreadSoulMarkerOverlay>();
        _overlays.RemoveOverlay<BossCommunionFogOverlay>();
        _overlays.RemoveOverlay<BossCommunionLanternOverlay>();
        _overlays.RemoveOverlay<BossVigilMarkOverlay>();
        _overlays.RemoveOverlay<BossVigilAshOverlay>();
        base.Shutdown();
    }
}
