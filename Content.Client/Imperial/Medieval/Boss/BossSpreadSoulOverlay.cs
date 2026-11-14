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

public sealed class BossSpreadSoulFloorOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    public BossSpreadSoulFloorOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes; _resources = resources; _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = BossOverlayLayers.Ground;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var impact, out var xform, out var metadata))
        {
            if (!impact.Impact || impact.SpreadCenters.Count == 0 || xform.MapID != args.MapId ||
                !_entities.EntityExists(impact.Grid) || !_entities.EntityExists(impact.Boss) ||
                !_prototypes.TryIndex(impact.Style, out var style) || style.SpreadSoulTexture is not { } path ||
                !_resources.TryGetResource<RSIResource>(path, out var resource) ||
                !resource.RSI.TryGetState("burst", out var state)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            var age = (now - impact.StartTime).TotalSeconds;
            if (age < 0 || age >= BossSpreadSoulVisuals.DurationSeconds) continue;
            var frame = state.GetFrame(RsiDirection.South, BossSpreadSoulVisuals.BurstFrame(age));
            foreach (var center in impact.SpreadCenters)
            {

                handle.SetTransform(Matrix3x2.CreateTranslation(new Vector2(center.X + 0.5f, center.Y + 0.5f)) *
                    _transform.GetWorldMatrix(impact.Grid));
                handle.DrawTextureRect(frame, BossSpreadSoulVisuals.Bounds(frame.Size), Color.White);
            }
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class BossSpreadSoulMarkerOverlay : Overlay
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

    public BossSpreadSoulMarkerOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _sprites = entities.System<SpriteSystem>();
        _prototypes = prototypes; _resources = resources; _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = 1000;
    }

    private Vector2 MarkerPosition(EntityUid target, SpriteComponent sprite, Matrix3x2 rotation)
    {
        var top = _sprites.GetLocalBounds((target, sprite)).Top + sprite.Offset.Y;
        return _transform.GetWorldPosition(target) + Vector2.TransformNormal(new Vector2(0,
            top + BossSpreadSoulVisuals.MarkerBottomExtent + BossSpreadSoulVisuals.MarkerGap), rotation);
    }

    private static void DrawFrame(DrawingHandleWorld handle, Texture frame, Vector2 position, Matrix3x2 rotation)
    {
        handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
        handle.DrawTextureRect(frame, BossSpreadSoulVisuals.Bounds(frame.Size), Color.White);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        handle.UseShader(_shader);
        var markers = _entities.AllEntityQueryEnumerator<BossActorMarkerComponent, TransformComponent, MetaDataComponent>();
        while (markers.MoveNext(out var uid, out var mark, out var xform, out var metadata))
        {
            if (mark.Kind != BossActorMarkerKind.Spread || xform.MapID != args.MapId ||
                !_entities.TryGetComponent<SpriteComponent>(mark.Target, out var sprite) || !sprite.Visible || sprite.ContainerOccluded ||
                !_entities.TryGetComponent<TransformComponent>(mark.Boss, out var boss) || boss.MapID != args.MapId ||
                !_prototypes.TryIndex(mark.Style, out var style) || style.SpreadSoulTexture is not { } path ||
                !_resources.TryGetResource<RSIResource>(path, out var resource)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            var elapsed = Math.Max(0, (now - mark.StartTime).TotalSeconds);
            var position = MarkerPosition(mark.Target, sprite, rotation) + Vector2.TransformNormal(new Vector2(0, BossSpreadSoulVisuals.MarkerBob(elapsed)), rotation);
            _positions[(mark.Boss, mark.Target)] = (position, now);              // keep each boss and target pair separate
            var specifier = new SpriteSpecifier.Rsi(path, "marker");
            DrawFrame(handle, _sprites.GetFrame(specifier, TimeSpan.FromSeconds(elapsed), loop: true), position, rotation);
        }
        var impacts = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        while (impacts.MoveNext(out var uid, out var impact, out var xform, out var metadata))
        {
            if (!impact.Impact || impact.SpreadTargets.Count == 0 || xform.MapID != args.MapId ||
                !_entities.EntityExists(impact.Boss) || !_prototypes.TryIndex(impact.Style, out var style) ||
                style.SpreadSoulTexture is not { } path || !_resources.TryGetResource<RSIResource>(path, out _)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            var elapsed = (now - impact.StartTime).TotalSeconds;
            if (elapsed < 0 || elapsed >= .08) continue;
            foreach (var target in impact.SpreadTargets)
            {
                var key = (impact.Boss, target);
                if (!_positions.TryGetValue(key, out var last))
                {
                    if (!_entities.TryGetComponent<SpriteComponent>(target, out var sprite)) continue;
                    last = (MarkerPosition(target, sprite, rotation), now);
                    _positions[key] = last;
                }
                DrawFrame(handle, _sprites.GetFrame(new SpriteSpecifier.Rsi(path, "snap"),
                    TimeSpan.FromSeconds(elapsed), loop: false), last.Position, rotation);
            }
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        foreach (var key in _positions.Keys.ToArray())
            if (!_entities.EntityExists(key.Boss) || (_timing.CurTime - _positions[key].Seen).TotalSeconds > 2)
                _positions.Remove(key);
    }
}

public sealed class BossSpreadSoulOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossSpreadSoulFloorOverlay(EntityManager, Prototypes, Resources, Timing);
        yield return new BossSpreadSoulMarkerOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
