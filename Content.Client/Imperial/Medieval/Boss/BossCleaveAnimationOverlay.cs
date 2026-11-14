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

public sealed class BossCleaveAnimationOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    public BossCleaveAnimationOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = 800;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var impact, out var transform, out var metadata))
        {
            if (!impact.Impact || impact.TankTarget != null || !BossCleaveVisuals.IsCleave(impact.Pattern) ||
                transform.MapID != args.MapId || !_entities.EntityExists(impact.Grid) || !_entities.EntityExists(impact.Boss) ||
                !_prototypes.TryIndex(impact.Style, out var style) || !style.CleaveLight) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            if (now < impact.StartTime || now >= impact.EndTime) continue;
            var elapsed = (float)(now - impact.StartTime).TotalSeconds;
            var cleave = BossTilePatterns.Danger(impact.Pattern, [], facing: impact.Facing,
                origin: impact.PatternOrigin, halfSize: impact.HalfSize).ToHashSet();
            var mask = MaskRows(impact.Danger.Where(cleave.Contains));
            if (mask.Length == 0) continue;
            var rotation = BossCleaveVisuals.Rotation(impact.Pattern, impact.Facing);
            var scale = impact.MovementCleave ? style.CleaveLightMovementScale : style.CleaveLightScale;
            if (!float.IsFinite(scale) || scale <= 0) continue;
            var initialRadius = BossCleaveVisuals.InitialRadius(impact.HalfSize);
            var reach = MathF.Max(initialRadius, MaximumRadius(mask, impact.PatternOrigin) + BossCleaveVisuals.CornerPadding);
            handle.SetTransform(_transform.GetWorldMatrix(impact.Grid));
            var currentRadius = BossCleaveVisuals.Radius(initialRadius, reach, elapsed) * scale;
            if (style.CleaveLightTexture is { } path &&
                float.IsFinite(style.CleaveLightRadiusPixels) && style.CleaveLightRadiusPixels > 0 &&
                _resources.TryGetResource<TextureResource>(path, out var resource))
            {
                var center = new Vector2(impact.PatternOrigin.X + 0.5f, impact.PatternOrigin.Y + 0.5f);
                handle.SetTransform(Matrix3x2.CreateRotation(rotation + MathF.PI / 2) *
                    Matrix3x2.CreateTranslation(center) * _transform.GetWorldMatrix(impact.Grid));
                handle.DrawTextureRect(resource.Texture,
                    TextureBounds(resource.Texture.Size, style.CleaveLightAnchorPixels, style.CleaveLightRadiusPixels, currentRadius),
                    Color.White.WithAlpha(BossCleaveVisuals.TextureAlpha(elapsed)));
                continue;
            }
            var washAlpha = BossCleaveVisuals.AppearanceAlpha(elapsed) *
                            Math.Clamp(1 - (elapsed - BossCleaveVisuals.ExpandSeconds) / BossCleaveVisuals.TrailSeconds, 0, 1);
            DrawWave(handle, impact.PatternOrigin, currentRadius / 2, currentRadius, rotation,
                style.PurpleColor.WithAlpha(washAlpha * 0.075f), elapsed, style.CleaveLightWaveArc);
            foreach (var sample in BossCleaveVisuals.Samples(elapsed))
            {
                var radius = BossCleaveVisuals.Radius(initialRadius, reach, sample.Time) * scale;
                var width = style.CleaveLightWidth * scale;
                DrawWave(handle, impact.PatternOrigin, radius, width * 1.25f, rotation,
                    style.PurpleColor.WithAlpha(sample.Alpha * 0.2f), elapsed, style.CleaveLightWaveArc, -width * 0.45f);
                DrawWave(handle, impact.PatternOrigin, radius, width, rotation,
                    style.CleaveLightBody.WithAlpha(sample.Alpha * (sample.Leading ? 0.8f : 0.35f)),
                    elapsed, style.CleaveLightWaveArc, -width / 2);
                if (!sample.Leading) continue;
                DrawWave(handle, impact.PatternOrigin, radius, width * 0.4f, rotation,
                    style.PurpleColor.WithAlpha(sample.Alpha * 0.7f), elapsed, style.CleaveLightWaveArc, -width * 0.2f);
                DrawWave(handle, impact.PatternOrigin, radius, 0.12f * scale, rotation,
                    style.CleaveLightCore.WithAlpha(sample.Alpha), elapsed, style.CleaveLightWaveArc, -0.06f * scale);
            }
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    public static Box2[] MaskRows(IEnumerable<Vector2i> danger)
        => BossOverlayGeometry.MaskRows(danger);

    public static float MaximumRadius(IReadOnlyList<Box2> mask, Vector2i origin)
        => BossOverlayGeometry.MaximumRadius(mask, origin);

    public static Box2 TextureBounds(Vector2i size, Vector2 anchor, float artRadius, float radius)
        => BossOverlayGeometry.TextureBounds(size, anchor, artRadius, radius);

    private static void DrawWave(DrawingHandleWorld handle, Vector2i origin,
        float radius, float width, float rotation, Color color, float elapsed, float arc, float inset = 0)
    {
        var vertices = StrokeVertices(origin, radius, width, rotation, color, elapsed, arc, inset);
        if (vertices.Length > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, vertices);
    }

    public static DrawVertexUV2DColor[] StrokeVertices(Vector2i origin, float radius, float width,
        float rotation, Color color, float elapsed, float arc = BossCleaveVisuals.ArcRadians, float inset = 0)
    {
        var mesh = StrokeMesh(origin, radius, width, rotation, arc, inset);
        var vertices = new DrawVertexUV2DColor[mesh.Length];
        var segments = mesh.Length / 6;
        var linear = Color.FromSrgb(color);
        for (var segment = 0; segment < segments; segment++)
            for (var corner = 0; corner < 6; corner++)
            {
                var fraction = (segment + (corner is 1 or 2 or 4 ? 1 : 0)) / (float)segments;
                var index = segment * 6 + corner;
                vertices[index] = new DrawVertexUV2DColor(mesh[index],
                    linear.WithAlpha(linear.A * BossCleaveVisuals.StrokeAlpha(fraction, elapsed)));
            }
        return vertices;
    }

    public static Vector2[] StrokeMesh(Vector2i origin, float radius, float width,
        float rotation, float arc = BossCleaveVisuals.ArcRadians, float inset = 0)
    {
        var center = new Vector2(origin.X + 0.5f, origin.Y + 0.5f);
        var triangles = new List<Vector2>();
        var segments = Math.Max(1, (int)MathF.Ceiling(64 * arc / MathF.PI));
        var startAngle = rotation + MathF.PI / 2 - arc / 2;
        for (var segment = 0; segment < segments; segment++)
        {
            var angle1 = startAngle + arc * segment / segments;
            var angle2 = startAngle + arc * (segment + 1) / segments;
            var axis1 = new Vector2(MathF.Cos(angle1), MathF.Sin(angle1));
            var axis2 = new Vector2(MathF.Cos(angle2), MathF.Sin(angle2));
            var outer = MathF.Max(0, radius + inset + width / 2);
            var inner = MathF.Max(0, outer - width);
            var innerPoint1 = center + axis1 * inner;
            var innerPoint2 = center + axis2 * inner;
            triangles.AddRange(new[]
            {
                innerPoint1, innerPoint2, center + axis2 * outer,
                innerPoint1, center + axis2 * outer, center + axis1 * outer
            });
        }
        return triangles.ToArray();
    }

}

public sealed class BossCleaveOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossCleaveAnimationOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
