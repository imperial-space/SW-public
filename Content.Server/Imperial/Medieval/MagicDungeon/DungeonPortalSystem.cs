using Content.Server.Imperial.Medieval.Procedural;
using Content.Shared.Imperial.Medieval.MagicDungeon;

namespace Content.Server.Imperial.Medieval.MagicDungeon;

public sealed class DungeonPortalSystem : SharedDungeonPortalSystem
{
    [Dependency] private readonly DungeonGenerationSystem _generationSystem = default!;

    protected override void SpawnPortal(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        base.SpawnPortal(portalFrame);

        var generationSize = portalFrame.Comp.BaseSize;
        _generationSystem.GenerateDungeon(generationSize.X, generationSize.Y, out portalFrame.Comp.DungeonMap, out portalFrame.Comp.CoordForSpawnList);
        Dirty(portalFrame);
    }
}
