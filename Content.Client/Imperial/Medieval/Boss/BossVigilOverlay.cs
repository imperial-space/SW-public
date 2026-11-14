using System.Collections.Generic;
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

public sealed class BossVigilMarkOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Unshaded = "unshaded";
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    public BossVigilMarkOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities; _prototypes = prototypes; _resources = resources; _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _shader = prototypes.Index(Unshaded).Instance();
        ZIndex = 1000;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var query = _entities.AllEntityQueryEnumerator<BossVigilComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var visual, out var xform, out var metadata))
        {
            if (xform.MapID != args.MapId ||
                !_prototypes.TryIndex(visual.Style, out var style) || style.VigilMarkTexture is not { } path ||
                !_resources.TryGetResource<RSIResource>(path, out var resource) ||
                !resource.RSI.TryGetState("strike", out var state)) continue;

            if (!visual.Resolved) continue;
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, metadata);
            var age = (now - visual.ResolveTime).TotalSeconds;
            if (age < 0 || age >= BossVigilVisuals.MarkSeconds) continue;
            var frame = state.GetFrame(RsiDirection.South, BossVigilVisuals.MarkFrame(age));
            foreach (var target in visual.Victims)
            {
                if (!_entities.TryGetComponent<TransformComponent>(target, out var body) ||
                    body.MapID != args.MapId || body.GridUid != visual.Grid ||
                    !_entities.TryGetComponent<SpriteComponent>(target, out var sprite) ||
                    !sprite.Visible || sprite.ContainerOccluded) continue;
                var position = _transform.GetWorldPosition(target) +
                    Vector2.TransformNormal(sprite.Offset + BossVigilVisuals.VictimAnchorOffset, rotation);
                handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
                handle.DrawTextureRect(frame, BossVigilVisuals.MarkBounds(frame.Size), Color.White);
            }
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class BossVigilAshOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;

    public BossVigilAshOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities; _prototypes = prototypes; _resources = resources; _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        ZIndex = (int)Content.Shared.DrawDepth.DrawDepth.Mobs;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var query = _entities.AllEntityQueryEnumerator<BossVigilComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var visual, out var xform, out var metadata))
        {
            if (xform.MapID != args.MapId || !_entities.TryGetComponent<SpriteComponent>(uid, out var sprite) ||
                sprite.ContainerOccluded || !_prototypes.TryIndex(visual.Style, out var style) ||
                style.VigilAshTexture is not { } path || !_resources.TryGetResource<RSIResource>(path, out var resource) ||
                !resource.RSI.TryGetState("vanish", out var state)) continue;
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, metadata);
            var index = BossVigilVisuals.AshFrame(visual, now);
            if (index < 0) continue;
            var frame = state.GetFrame(RsiDirection.South, index);
            var size = frame.Size / 32f;
            var position = _transform.GetWorldPosition(uid) + Vector2.TransformNormal(sprite.Offset, rotation);
            handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
            handle.DrawTextureRect(frame, Box2.FromDimensions(-size / 2, size), Color.White);
        }
        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class BossVigilOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossVigilMarkOverlay(EntityManager, Prototypes, Resources, Timing);
        yield return new BossVigilAshOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
