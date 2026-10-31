using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossCommunionFogOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _edgeShader;

    public BossCommunionFogOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes; _resources = resources; _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("RevenantCommunionFog").InstanceUnique();
        _edgeShader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = 700;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {

        var active = new Dictionary<EntityUid, (BossTelegraphComponent Visual, TimeSpan Now, float Opacity)>();
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var visual, out var xform, out var metadata))
        {
            if (xform.MapID != args.MapId || visual.StackCenter == null || !_entities.EntityExists(visual.Grid) ||
                !_entities.TryGetComponent<TransformComponent>(visual.Boss, out var boss) || boss.MapID != args.MapId ||
                !_prototypes.TryIndex(visual.Style, out var style) || !BossCommunionVisuals.Enabled(style, visual.Pattern)) continue;
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, metadata);
            var opacity = BossCommunionVisuals.FogOpacity(visual, now);
            if (opacity <= 0 || now < visual.StartTime || now >= visual.EndTime) continue;
            if (!active.TryGetValue(visual.Boss, out var previous) || opacity > previous.Opacity ||
                opacity == previous.Opacity && visual.Impact)
                active[visual.Boss] = (visual, now, opacity);
        }
        var handle = args.WorldHandle;
        foreach (var (visual, now, opacity) in active.Values)
        {
            var style = _prototypes.Index(visual.Style);
            if (style.CommunionFogTexture is not { } path || !_resources.TryGetResource<RSIResource>(path, out var resource) ||
                !resource.RSI.TryGetState("fog", out var state)) continue;
            var refuge = BossCommunionVisuals.SafeBounds(visual.StackCenter!.Value, visual.HalfSize);
            _shader.SetParameter("refuge_min", refuge.BottomLeft);
            _shader.SetParameter("refuge_max", refuge.TopRight);
            _shader.SetParameter("feather", BossCommunionVisuals.FeatherTiles);
            handle.UseShader(_shader);
            handle.SetTransform(_transform.GetWorldMatrix(visual.Grid));
            var visible = _transform.GetInvWorldMatrix(visual.Grid).TransformBox(args.WorldBounds);
            var frame = state.GetFrame(RsiDirection.South, BossCommunionVisuals.FogFrame(now - visual.AnimationStartTime));
            var span = BossCommunionVisuals.FogSpanTiles;
            var origin = .5f - span / 2;
            for (var y = (int)MathF.Floor((visible.Bottom - origin) / span); y <= (int)MathF.Floor((visible.Top - origin) / span); y++)
                for (var x = (int)MathF.Floor((visible.Left - origin) / span); x <= (int)MathF.Floor((visible.Right - origin) / span); x++)
                    handle.DrawTextureRect(frame, Box2.FromDimensions(new Vector2(origin + x * span, origin + y * span), new Vector2(span)),
                        Color.White.WithAlpha(opacity));

            if (visual.Impact && style.CommunionEdgeTexture is { } edgePath &&
                _resources.TryGetResource<RSIResource>(edgePath, out var edgeResource) &&
                edgeResource.RSI.TryGetState("ward", out var edgeState))
            {
                handle.UseShader(_edgeShader);
                var edge = edgeState.GetFrame(RsiDirection.South,
                    BossCommunionVisuals.EdgeFrame((now - visual.StartTime).TotalSeconds));
                foreach (var patch in BossCommunionVisuals.EdgePatches(refuge))
                    handle.DrawTextureRectRegion(edge, patch.Bounds, Color.White, patch.Region);
            }
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

public sealed class BossCommunionLanternOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly SpriteSystem _sprites;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    private readonly Dictionary<(EntityUid Boss, EntityUid Target), (Vector2 Position, TimeSpan Seen)> _positions = new();

    public BossCommunionLanternOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _sprites = entities.System<SpriteSystem>();
        _prototypes = prototypes; _resources = resources; _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = 1050;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        handle.UseShader(_shader);
        var drawn = new HashSet<(EntityUid Boss, EntityUid Target)>();
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var visual, out var xform, out var metadata))
        {
            if (xform.MapID != args.MapId || visual.StackTarget is not { } target || visual.StackCenter is not { } tile ||
                !_entities.EntityExists(visual.Grid) || !_entities.TryGetComponent<TransformComponent>(visual.Boss, out var boss) ||
                boss.MapID != args.MapId || !_prototypes.TryIndex(visual.Style, out var style) ||
                !BossCommunionVisuals.Enabled(style, visual.Pattern) || style.CommunionLanternTexture is not { } path ||
                !_resources.TryGetResource<RSIResource>(path, out _)) continue;
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, metadata);
            if (now < visual.StartTime || now >= visual.EndTime) continue;
            var height = _entities.TryGetComponent<SpriteComponent>(target, out var sprite)
                ? _sprites.GetLocalBounds((target, sprite)).Top + sprite.Offset.Y : .75f;
            var key = (visual.Boss, target);
            Vector2 position;
            if (visual.Impact)
            {
                var frozen = Vector2.Transform(new Vector2(tile.X + .5f, tile.Y + .5f), _transform.GetWorldMatrix(visual.Grid));
                position = _positions.TryGetValue(key, out var last) && last.Seen >= visual.StartTime - TimeSpan.FromSeconds(.5)
                    ? last.Position : frozen + Vector2.TransformNormal(new Vector2(0, height + .8f), rotation);
                _positions[key] = (position, now);
            }
            else
            {
                if (!_entities.TryGetComponent<TransformComponent>(target, out var body) || body.MapID != args.MapId ||
                    sprite == null || !sprite.Visible || sprite.ContainerOccluded) continue;
                position = _transform.GetWorldPosition(target) + Vector2.TransformNormal(new Vector2(0, height + .8f), rotation);
                _positions[key] = (position, now);
            }
            var age = (now - visual.StartTime).TotalSeconds;
            var dust = visual.Impact && age >= BossCommunionVisuals.HoldSeconds;
            var elapsed = dust ? age - BossCommunionVisuals.HoldSeconds : (now - visual.AnimationStartTime).TotalSeconds;
            if (dust && elapsed >= .96) continue;
            var frame = _sprites.GetFrame(new SpriteSpecifier.Rsi(path, dust ? "dissolve" : "marker"),
                TimeSpan.FromSeconds(Math.Max(0, elapsed)), loop: !dust);
            handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
            handle.DrawTextureRect(frame, new Box2(-1, -1, 1, 1), Color.White);
            drawn.Add(key);
        }

        var marks = _entities.AllEntityQueryEnumerator<BossActorMarkerComponent, TransformComponent, MetaDataComponent>();
        while (marks.MoveNext(out var uid, out var mark, out var xform, out var metadata))
        {
            if (mark.Kind != BossActorMarkerKind.Stack || xform.MapID != args.MapId ||
                drawn.Contains((mark.Boss, mark.Target)) || !_prototypes.TryIndex(mark.Style, out var style) ||
                !BossCommunionVisuals.ReplacesLegacyCue(style, mark.Kind) || style.CommunionLanternTexture is not { } path ||
                !_entities.TryGetComponent<TransformComponent>(mark.Target, out var body) || body.MapID != args.MapId ||
                !_entities.TryGetComponent<SpriteComponent>(mark.Target, out var sprite) || !sprite.Visible || sprite.ContainerOccluded ||
                !_resources.TryGetResource<RSIResource>(path, out _)) continue;
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, metadata);
            var height = _sprites.GetLocalBounds((mark.Target, sprite)).Top + sprite.Offset.Y;
            var position = _transform.GetWorldPosition(body) + Vector2.TransformNormal(new Vector2(0, height + .8f), rotation);
            _positions[(mark.Boss, mark.Target)] = (position, now);
            var frame = _sprites.GetFrame(new SpriteSpecifier.Rsi(path, "marker"),
                now - mark.StartTime, loop: true);
            handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
            handle.DrawTextureRect(frame, new Box2(-1, -1, 1, 1), Color.White);
            drawn.Add((mark.Boss, mark.Target));
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        foreach (var key in _positions.Keys.ToArray())
            if (!_entities.EntityExists(key.Boss) || (_timing.CurTime - _positions[key].Seen).TotalSeconds > 3)
                _positions.Remove(key);
    }
}

public sealed class BossCommunionOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossCommunionFogOverlay(EntityManager, Prototypes, Resources, Timing);
        yield return new BossCommunionLanternOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
