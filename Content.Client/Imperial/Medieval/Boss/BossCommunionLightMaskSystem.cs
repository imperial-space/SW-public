using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossCommunionLightMaskSystem : EntitySystem
{
    [Dependency] private readonly PointLightSystem _lights = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private readonly Dictionary<EntityUid, string> _paths = new();

    public override void FrameUpdate(float frameTime)
    {
        var query = AllEntityQuery<BossTelegraphComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var visual, out var light))
        {
            if (visual.StackCenter is not { } center || !BossCommunionVisuals.UsesFog(visual) ||
                !Exists(visual.Grid) || !_prototypes.TryIndex(visual.Style, out var style) ||
                !BossCommunionVisuals.Enabled(style, visual.Pattern)) continue;
            var refuge = BossCommunionVisuals.SafeBounds(center, visual.HalfSize);
            var width = (int)MathF.Round(refuge.Width);
            var height = (int)MathF.Round(refuge.Height);
            var path = $"/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/communion_light_{width}x{height}.png";
            if (!_paths.TryGetValue(uid, out var previous) || previous != path)
            {
                _lights.SetMask(path, light);
                _paths[uid] = path;
            }
        }
        foreach (var uid in _paths.Keys.Where(uid => !Exists(uid)).ToArray()) _paths.Remove(uid);
    }
}
