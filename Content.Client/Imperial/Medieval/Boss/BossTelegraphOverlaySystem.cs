using System.Collections.Generic;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossTelegraphOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossFloorTelegraphOverlay(EntityManager, Prototypes, Resources, Timing);
        yield return new BossActorMarkerOverlay(EntityManager, Prototypes, Resources, Timing, Player);
    }
}
