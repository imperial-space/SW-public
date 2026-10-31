using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossHealthBarOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IResourceCache _resources;
    private readonly SharedTransformSystem _transform;
    private readonly ExamineSystemShared _examine;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public BossHealthBarOverlay(IEntityManager entities, IResourceCache resources)
    {
        _entities = entities;
        _resources = resources;
        _transform = entities.System<SharedTransformSystem>();
        _examine = entities.System<ExamineSystemShared>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eye = args.Viewport.Eye;
        var rotation = Matrix3Helpers.CreateRotation(-(eye?.Rotation ?? Angle.Zero));
        var xforms = _entities.GetEntityQuery<TransformComponent>();
        var query = _entities.AllEntityQueryEnumerator<BossHealthBarComponent, SpriteComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var bar, out var sprite, out var metadata))
        {
            if (!bar.ShowOverhead || !bar.Visible || bar.MaxHp <= 0f || bar.CurrentHp <= 0f ||
                !float.IsFinite(bar.CurrentHp) || !float.IsFinite(bar.MaxHp) ||
                bar.Size.X <= 0f || bar.Size.Y <= 0f ||
                !sprite.Visible || sprite.ContainerOccluded ||
                (metadata.Flags & MetaDataFlags.Detached) != 0 ||
                !xforms.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId)
                continue;

            var position = _transform.GetWorldPosition(xform, xforms);
            var matrix = rotation * Matrix3Helpers.CreateTranslation(position);
            var size = bar.Size / EyeManager.PixelsPerMeter;
            var center = bar.Offset / EyeManager.PixelsPerMeter;
            var bounds = Box2.FromDimensions(center - size / 2f, size);

            if (!matrix.TransformBox(bounds).Intersects(args.WorldAABB))
                continue;

            if (eye is { DrawFov: true } && !_examine.InRangeUnOccluded(
                    eye.Position, new MapCoordinates(position, args.MapId), 0f,
                    uid, static (candidate, boss) => candidate == boss))
                continue;

            handle.SetTransform(matrix);
            DrawBar(handle, bounds, bar);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawBar(DrawingHandleWorld handle, Box2 bounds, BossHealthBarComponent bar)
    {
        var frame = GetTexture(bar.FrameTexture);
        if (frame == null)
            handle.DrawRect(bounds, bar.BorderColor);

        var border = Math.Clamp(bar.BorderWidth / EyeManager.PixelsPerMeter, 0f, Math.Min(bounds.Width, bounds.Height) / 2f);
        var inner = bounds.Enlarged(-border);

        if (inner.Width > 0f && inner.Height > 0f)
        {
            if (GetTexture(bar.BackgroundTexture) is { } background)
                handle.DrawTextureRect(background, inner);
            else
                handle.DrawRect(inner, bar.BackgroundColor);

            var ratio = Math.Clamp(bar.CurrentHp / bar.MaxHp, 0f, 1f);           // health fill from empty to full
            var fillBounds = Box2.FromDimensions(inner.BottomLeft, new Vector2(inner.Width * ratio, inner.Height));
            if (GetTexture(bar.FillTexture) is { } fill)
            {

                var region = new UIBox2(0f, 0f, fill.Width * ratio, fill.Height);
                handle.DrawTextureRectRegion(fill, fillBounds, subRegion: region);
            }
            else
            {
                handle.DrawRect(fillBounds, bar.Color);
            }
        }

        if (frame != null)
            handle.DrawTextureRect(frame, bounds);
    }

    private Texture? GetTexture(ResPath? path)
    {
        return path is { } resourcePath && _resources.TryGetResource<TextureResource>(resourcePath, out var resource)
            ? resource.Texture
            : null;
    }
}

public sealed class BossHealthBarNameOverlay : Overlay
{
    private const float TextScale = 6f / 32f;
    private const float TextGap = 2f;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly ExamineSystemShared _examine;
    private readonly Font _font;
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public BossHealthBarNameOverlay(IEntityManager entities, IResourceCache resources)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _examine = entities.System<ExamineSystemShared>();
        _font = new VectorFont(resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 32);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null) return;
        var handle = args.ScreenHandle;
        var eye = args.Viewport.Eye;
        var rotation = Matrix3Helpers.CreateRotation(-(eye?.Rotation ?? Angle.Zero));
        var worldToScreen = args.ViewportControl.GetWorldToScreenMatrix();
        var xforms = _entities.GetEntityQuery<TransformComponent>();
        var query = _entities.AllEntityQueryEnumerator<BossHealthBarComponent, SpriteComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var bar, out var sprite, out var metadata))
        {
            if (!_entities.HasComponent<BossPresentationComponent>(uid) || !bar.ShowOverhead || !bar.Visible ||
                bar.MaxHp <= 0 || bar.CurrentHp <= 0 || !float.IsFinite(bar.MaxHp) || !float.IsFinite(bar.CurrentHp) ||
                !float.IsFinite(bar.Size.X) || !float.IsFinite(bar.Size.Y) ||
                !float.IsFinite(bar.Offset.X) || !float.IsFinite(bar.Offset.Y) || bar.Size.X <= 0 || bar.Size.Y <= 0 ||
                !sprite.Visible || sprite.ContainerOccluded || (metadata.Flags & MetaDataFlags.Detached) != 0 ||
                !xforms.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId)
                continue;

            var text = metadata.EntityName.Replace('\r', ' ').Replace('\n', ' ');
            var position = _transform.GetWorldPosition(xform, xforms);
            var matrix = rotation * Matrix3Helpers.CreateTranslation(position);
            var left = bar.Offset.X - bar.Size.X / 2f;
            var top = bar.Offset.Y + bar.Size.Y / 2f;
            var height = TextGap + _font.GetLineHeight(1) * TextScale;
            var width = 0f;
            foreach (var rune in text.EnumerateRunes())
                width += _font.GetCharMetrics(rune, 1)?.Advance ?? 0;
            var bounds = new Box2(left / EyeManager.PixelsPerMeter, (bar.Offset.Y - bar.Size.Y / 2f) / EyeManager.PixelsPerMeter,
                (left + Math.Max(bar.Size.X, width * TextScale)) / EyeManager.PixelsPerMeter, (top + height) / EyeManager.PixelsPerMeter);
            if (!matrix.TransformBox(bounds).Intersects(args.WorldAABB)) continue;
            if (eye is { DrawFov: true } && !_examine.InRangeUnOccluded(eye.Position,
                new MapCoordinates(position, args.MapId), 0f, uid, static (candidate, boss) => candidate == boss))
                continue;

            var pixelToScreen = Matrix3x2.CreateScale(1f / EyeManager.PixelsPerMeter,
                -1f / EyeManager.PixelsPerMeter) * matrix * worldToScreen;
            handle.SetTransform(Matrix3x2.CreateScale(TextScale) * pixelToScreen);
            var origin = new Vector2(left, -top - height) / TextScale;
            handle.DrawString(_font, origin + new Vector2(0.5f / TextScale), text, 1, Color.Black.WithAlpha(0.9f));
            handle.DrawString(_font, origin, text, 1, Color.White);
        }
        handle.SetTransform(Matrix3x2.Identity);
    }
}
