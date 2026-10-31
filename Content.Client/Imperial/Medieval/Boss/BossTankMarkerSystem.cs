using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.CombatMode;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossTankMarkerSystem : EntitySystem
{
    [Dependency]
    private readonly IOverlayManager _overlays = default!;
    [Dependency]
    private readonly IPrototypeManager _prototypes = default!;
    [Dependency]
    private readonly IResourceCache _resources = default!;
    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new BossTankMarkerOverlay(EntityManager, _prototypes, _resources));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BossTankMarkerOverlay>();
        base.Shutdown();
    }
}

public sealed class BossTankMarkerOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    public BossTankMarkerOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _sprites = entities.System<SpriteSystem>();
        _prototypes = prototypes;
        _resources = resources;
        ZIndex = 1000;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var query = _entities.AllEntityQueryEnumerator<BossActorMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mark, out var xform))
        {
            if (mark.Kind != BossActorMarkerKind.Tank || xform.MapID != args.MapId || _entities.HasComponent<SpriteComponent>(uid) || !_entities.TryGetComponent<SpriteComponent>(mark.Target, out var sprite) || !sprite.Visible || sprite.ContainerOccluded || !_entities.TryGetComponent<TransformComponent>(mark.Boss, out var bossXform) || bossXform.MapID != args.MapId || !_prototypes.TryIndex(mark.Style, out var style))
                continue;
            var position = _transform.GetWorldPosition(mark.Target);
            handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
            var size = style.TankMarkerSize;
            var bodyBounds = _entities.TryGetComponent<StatusIconComponent>(mark.Target, out var status) && status.Bounds is { } statusBounds ? statusBounds : _sprites.GetLocalBounds((mark.Target, sprite));
            var iconWidth = 8f;
            var iconOffset = 0f;
            if (_entities.TryGetComponent<CombatModeComponent>(mark.Target, out var combat) && _prototypes.TryIndex<DebugIconPrototype>(combat.CombatModeIcon, out var icon))
            {
                iconWidth = _sprites.Frame0(icon.Icon).Width;
                iconOffset = icon.Offset;
            }

            var offset = style.TankMarkerOffset ?? new Vector2(0, 0.05f);
            var center = new Vector2(-(bodyBounds.Width + sprite.Offset.X) / 2f + iconWidth / (2f * EyeManager.PixelsPerMeter), (bodyBounds.Height + sprite.Offset.Y) / 2f + iconOffset / EyeManager.PixelsPerMeter + size.Y / 2f) + offset;
            var bounds = Box2.FromDimensions(center - size / 2, size);
            if (style.TankTexture is { } path && _resources.TryGetResource<TextureResource>(path, out var resource))
            {
                if (style.TankTextureContentSize is { } contentSize)
                    bounds = BossSpriteCanvas.ExpandBounds(bounds, contentSize, resource.Texture.Size);
                handle.DrawTextureRect(resource.Texture, bounds);
                continue;
            }

            var points = new[]
            {
                new Vector2(-0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.4f, -0.15f),
                new Vector2(0, -0.5f),
                new Vector2(-0.4f, -0.15f)
            };
            for (var i = 0; i < points.Length; i++)
                handle.DrawLine(center + points[i] * size, center + points[(i + 1) % points.Length] * size, style.TankColor);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
