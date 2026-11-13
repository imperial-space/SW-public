// Imperial medieval start
using System.Linq;
using Content.Shared.Atmos;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
namespace Content.Shared.Explosion.EntitySystems;


public abstract partial class SharedExplosionSystem
{
    [Dependency] private readonly IMapManager _explosionMaps = default!;
    [Dependency] private readonly SharedMapSystem _explosionMap = default!;
    [Dependency] private readonly SharedTransformSystem _explosionTransform = default!;
    [Dependency] private readonly IPrototypeManager _explosionPrototypes = default!;
    public const ushort DefaultTileSize = 1;
    protected virtual int FloodMaxIterations => 64;
    protected virtual int FloodMaxArea => 4096;
    protected virtual bool RejectTruncatedFlood => false;
    protected readonly Dictionary<string, int> _explosionTypes = new();
    protected readonly Dictionary<EntityUid, Dictionary<Vector2i, TileData>> _airtightMap = new();
    private void InitializePropagation()
    {
        var explosionTypes = _explosionPrototypes
            .EnumeratePrototypes<ExplosionPrototype>()
            .ToList();

        explosionTypes.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));

        foreach (var type in explosionTypes)
            _explosionTypes.Add(type.ID, _explosionTypes.Count);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
        SubscribeLocalEvent<GridStartupEvent>(OnGridStartup);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }
    public struct TileData(float[] explosionTolerance, AtmosDirection blockedDirections)
    {
        public float[] ExplosionTolerance = explosionTolerance;
        public AtmosDirection BlockedDirections = blockedDirections;
    }
    /// <summary>
    ///     Find the strength needed to generate an explosion of a given radius. More useful for radii larger then 4, when the explosion becomes less "blocky".
    /// </summary>
    /// <remarks>
    ///     This assumes the explosion is in a vacuum / unobstructed. Given that explosions are not perfectly
    ///     circular, here radius actually means the sqrt(Area/pi), where the area is the total number of tiles
    ///     covered by the explosion. Until you get to radius 30+, this is functionally equivalent to the
    ///     actual radius.
    /// </remarks>
    public float RadiusToIntensity(float radius, float slope, float maxIntensity = 0)
    {
        // If you consider the intensity at each tile in an explosion to be a height. Then a circular explosion is
        // shaped like a cone. So total intensity is like the volume of a cone with height = slope * radius. Of
        // course, as the explosions are not perfectly circular, this formula isn't perfect, but the formula works
        // reasonably well.

        // This should actually use the formula for the volume of a distorted octagonal frustum. But this is good
        // enough.

        var coneVolume = slope * MathF.PI / 3 * MathF.Pow(radius, 3);

        if (maxIntensity <= 0 || slope * radius < maxIntensity)
            return coneVolume;

        // This explosion is limited by the maxIntensity.
        // Instead of a cone, we have a conical frustum.

        // Subtract the volume of the missing cone segment, with height:
        var h = slope * radius - maxIntensity;
        return coneVolume - h * MathF.PI / 3 * MathF.Pow(h / slope, 2);
    }

    /// <summary>
    ///     Inverse formula for <see cref="RadiusToIntensity"/>
    /// </summary>
    public float IntensityToRadius(float totalIntensity, float slope, float maxIntensity)
    {
        // max radius to avoid being capped by max-intensity
        var r0 = maxIntensity / slope;

        // volume at r0
        var v0 = RadiusToIntensity(r0, slope);

        if (totalIntensity <= v0)
        {
            // maxIntensity is a non-issue, can use simple inverse formula
            return MathF.Cbrt(3 * totalIntensity / (slope * MathF.PI));
        }

        return r0 * (MathF.Sqrt(12 * totalIntensity / v0 - 3) / 6 + 0.5f);
    }

}
// Imperial medieval end
