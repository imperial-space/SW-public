using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossCircularSweepOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    public BossCircularSweepOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("RevenantCircularSweep").InstanceUnique();
        ZIndex = 800;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var impact, out var transform, out var metadata))
        {
            if (!impact.Impact || impact.TankTarget != null || !BossCircularSweepVisuals.IsCircular(impact.Pattern) ||
                transform.MapID != args.MapId || !_entities.EntityExists(impact.Grid) || !_entities.EntityExists(impact.Boss) ||
                !_prototypes.TryIndex(impact.Style, out var style)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            if (now < impact.StartTime || now >= impact.EndTime) continue;
            if (BossCircularSweepVisuals.Texture(style, impact.Pattern, impact.SecondPhase) is not { } path ||
                !_resources.TryGetResource<TextureResource>(path, out var resource)) continue;
            var close = BossCircularSweepVisuals.IsClose(impact.Pattern, impact.SecondPhase);
            var anchor = close ? style.CloseSweepAnchorPixels : style.DonutSweepAnchorPixels;
            var artRadius = close ? style.CloseSweepRadiusPixels : style.DonutSweepRadiusPixels;
            if (!float.IsFinite(artRadius) || artRadius <= 0) continue;
            var primary = BossTilePatterns.Danger(impact.Pattern, [], secondPhase: impact.SecondPhase,
                facing: impact.Facing, origin: impact.PatternOrigin, halfSize: impact.HalfSize).ToHashSet();
            var mask = BossOverlayGeometry.MaskRows(impact.Danger.Where(primary.Contains));
            if (mask.Length == 0) continue;
            var radius = BossOverlayGeometry.MaximumRadius(mask, impact.PatternOrigin) +
                         (close ? 0 : BossCleaveVisuals.CornerPadding);
            var elapsed = (float)(now - impact.StartTime).TotalSeconds;
            var size = resource.Texture.Size;
            _shader.SetParameter("anchor_uv", new Vector2(anchor.X / size.X, anchor.Y / size.Y));
            _shader.SetParameter("texture_size", new Vector2(size.X, size.Y));
            _shader.SetParameter("progress", BossCleaveVisuals.Progress(elapsed));
            _shader.SetParameter("start_angle", BossCircularSweepVisuals.StartAngle);
            _shader.SetParameter("sweep_angle", BossCircularSweepVisuals.SweepAngle);
            _shader.SetParameter("feather_angle", BossCircularSweepVisuals.FeatherAngle);
            handle.UseShader(_shader);
            var center = new Vector2(impact.PatternOrigin.X + 0.5f, impact.PatternOrigin.Y + 0.5f); // center the effect on the mechanic tile
            handle.SetTransform(Matrix3x2.CreateTranslation(center) * _transform.GetWorldMatrix(impact.Grid));
            handle.DrawTextureRect(resource.Texture,
                BossOverlayGeometry.TextureBounds(size, anchor, artRadius, radius),
                Color.White.WithAlpha(BossCleaveVisuals.TextureAlpha(elapsed)));
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }
}

public sealed class BossCircularSweepOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossCircularSweepOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
