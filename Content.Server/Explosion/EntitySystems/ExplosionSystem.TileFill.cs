// Imperial medieval start - native propagation moved to shared; the admin preview stays server-side.
using Content.Shared.Administration;
using Content.Shared.Explosion.Components;
using Robust.Shared.Timing;
namespace Content.Server.Explosion.EntitySystems;
public sealed partial class ExplosionSystem
{
    public ExplosionVisualsState? GenerateExplosionPreview(SpawnExplosionEuiMsg.PreviewRequest request)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var results = GetExplosionTiles(
            request.Epicenter,
            request.TypeId,
            request.TotalIntensity,
            request.IntensitySlope,
            request.MaxIntensity);

        if (results == null)
            return null;

        var (area, iterationIntensity, spaceData, gridData, spaceMatrix) = results.Value;

        Log.Info($"Generated explosion preview with {area} tiles in {stopwatch.Elapsed.TotalMilliseconds}ms");

        Dictionary<NetEntity, Dictionary<int, List<Vector2i>>> tileLists = new();
        foreach (var (grid, data) in gridData)
        {
            tileLists.Add(GetNetEntity(grid), data.TileLists);
        }

        return new ExplosionVisualsState(
            request.Epicenter,
            request.TypeId,
            iterationIntensity,
            spaceData?.TileLists,
            tileLists, spaceMatrix,
            spaceData?.TileSize ?? DefaultTileSize
            );
    }
}
// Imperial medieval end
