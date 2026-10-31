using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossConstellationOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    public BossConstellationOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = BossOverlayLayers.Ground;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var impact, out var transform, out var metadata))
        {
            if (!impact.Impact || impact.TankTarget != null || transform.MapID != args.MapId ||
                !_entities.EntityExists(impact.Grid) || !_entities.EntityExists(impact.Boss) ||
                !_prototypes.TryIndex(impact.Style, out var style))
                continue;
            if (style.ConstellationBeam is not { } animation ||
                BossConstellationVisuals.Texture(style, impact.Pattern, impact.HalfSize) is not { } path ||
                !_resources.TryGetResource<RSIResource>(path, out var resource))
                continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            if (now < impact.StartTime || now >= impact.EndTime)
                continue;
            var elapsed = (now - impact.StartTime).TotalSeconds;
            var index = BossConstellationVisuals.FrameIndex(elapsed);
            var tint = BossConstellationVisuals.IsBlue(impact.Pattern, impact.SecondPhase)
                ? Color.FromHex("#1E4D83") : Color.FromHex("#6420A5");
            handle.SetTransform(_transform.GetWorldMatrix(impact.Grid));        // draw in arena space so rotated maps still line up
            foreach (var row in BossOverlayGeometry.MaskRows(impact.Danger))
                handle.DrawRect(row, tint.WithAlpha(BossConstellationVisuals.ScorchOpacity(elapsed)));

            var allowedAreas = BossConstellationVisuals.AllowedAreas(impact.Danger, impact.HalfSize);
            foreach (var beam in BossConstellationVisuals.Beams(impact.Pattern, impact.SecondPhase, impact.HalfSize))
            {
                var width = (int)beam.Width;                                    // reuse the beam sprite for each lane
                if (!resource.RSI.TryGetState(BossConstellationVisuals.BeamState(impact.Pattern, impact.SecondPhase, width), out var state))
                    continue;
                var frame = state.GetFrame(RsiDirection.South, Math.Min(index, state.DelayCount - 1));
                BossBeamRenderer.Draw(handle, frame, beam, animation, width, allowedAreas);
            }
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class BossConstellationOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossConstellationOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
