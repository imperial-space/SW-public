using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossCommunionLightSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    private readonly Dictionary<EntityUid, float> _bossFactors = new();

    public override void Initialize()
    {
        UpdatesBefore.Add(typeof(BossLightSystem));
        SubscribeLocalEvent<BossLightFactorEvent>(OnFactor);
    }

    private void OnFactor(ref BossLightFactorEvent args) => args.Factor *= FactorFor(args.Entity);

    public float FactorFor(EntityUid boss) => _bossFactors.GetValueOrDefault(boss, 1);

    public override void Update(float frameTime)
    {
        _bossFactors.Clear();
        var query = AllEntityQuery<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var visual, out var xform, out var metadata))
        {
            if (!_prototypes.TryIndex(visual.Style, out var style) || !BossCommunionVisuals.Enabled(style, visual.Pattern)) continue;
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, metadata);
            if (visual.StackCenter is not { } center || !TryComp<TransformComponent>(visual.Boss, out var boss) ||
                boss.MapUid != xform.MapUid || !Exists(visual.Grid) || now < visual.StartTime || now >= visual.EndTime ||
                !BossCommunionVisuals.UsesFog(visual))
            {
                if (_lights.TryGetLight(uid, out var expired)) _lights.SetEnabled(uid, false, expired);
                continue;
            }
            var factor = BossCommunionVisuals.BossLightFactor(visual, now);
            _bossFactors[visual.Boss] = Math.Min(FactorFor(visual.Boss), factor);
            var refuge = BossCommunionVisuals.SafeBounds(center, visual.HalfSize);
            var light = _lights.EnsureLight(uid);
            var offset = refuge.Center - new Vector2(.5f);
            if (light.Offset != offset)
            {
                light.Offset = offset;
                Dirty(uid, light);
            }
            _lights.SetRadius(uid, BossCommunionVisuals.NativeLightRadius(refuge), light);
            _lights.SetColor(uid, BossCommunionVisuals.LanternColor, light);
            _lights.SetCastShadows(uid, false, light);
            _lights.SetFalloff(uid, .7f, light);
            _lights.SetEnergy(uid, BossCommunionVisuals.LightEnergy(visual, now), light);
            _lights.SetEnabled(uid, light.Energy > 0, light);
        }
    }
}
