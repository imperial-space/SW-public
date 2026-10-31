using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossCastBarOverlay : Overlay
{
    private const float TextGap = 1.5f;
    private const float LabelPadding = 2f;
    private const float FontScale = 1f;

    private const float TextScale = 4.5f / 32f;
    private readonly IEntityManager _entities;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly ExamineSystemShared _examine;
    private readonly MetaDataSystem _metadata;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public BossCastBarOverlay(IEntityManager entities, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _resources = resources;
        _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
        _examine = entities.System<ExamineSystemShared>();
        _metadata = entities.System<MetaDataSystem>();
        _font = new VectorFont(resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 32);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null) return;
        var handle = args.ScreenHandle;
        var worldToScreen = args.ViewportControl.GetWorldToScreenMatrix();
        var eye = args.Viewport.Eye;
        var rotation = Matrix3Helpers.CreateRotation(-(eye?.Rotation ?? Angle.Zero));
        var xforms = _entities.GetEntityQuery<TransformComponent>();
        var query = _entities.AllEntityQueryEnumerator<BossCastBarComponent, SpriteComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var bar, out var sprite, out var metadata))
        {
            if (!bar.Casting || !bar.Visible || bar.EndTime <= bar.StartTime ||
                !float.IsFinite(bar.Size.X) || !float.IsFinite(bar.Size.Y) ||
                !float.IsFinite(bar.Offset.X) || !float.IsFinite(bar.Offset.Y) ||
                bar.Size.X <= 0f || bar.Size.Y <= 0f ||
                (!sprite.Visible && bar.Anchor == null) || sprite.ContainerOccluded ||
                (metadata.Flags & MetaDataFlags.Detached) != 0 ||
                !xforms.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId)
                continue;

            var anchor = bar.Anchor is { } selected && xforms.TryGetComponent(selected, out var anchorXform) &&
                anchorXform.MapID == args.MapId ? selected : uid;
            var position = _transform.GetWorldPosition(anchor);
            var matrix = rotation * Matrix3Helpers.CreateTranslation(position);
            var size = bar.Size / EyeManager.PixelsPerMeter;
            var center = bar.Offset / EyeManager.PixelsPerMeter;
            var bounds = Box2.FromDimensions(center - size / 2f, size);

            var label = bar.GetLocalizedCastName().Replace('\r', ' ').Replace('\n', ' ');
            var labelWidth = Math.Max(bar.Size.X, LabelPadding + MeasureText(label) + 1f);
            var labelHeight = (TextGap + _font.GetLineHeight(FontScale) * TextScale +
                LabelPadding * 2f + 1f) / EyeManager.PixelsPerMeter;
            var fullBounds = new Box2(bounds.Left, bounds.Bottom - labelHeight,
                bounds.Left + labelWidth / EyeManager.PixelsPerMeter, bounds.Top);

            if (!matrix.TransformBox(fullBounds).Intersects(args.WorldAABB))
                continue;

            if (eye is { DrawFov: true } && !_examine.InRangeUnOccluded(
                    eye.Position, new MapCoordinates(position, args.MapId), 0f,
                    uid, static (candidate, boss) => candidate == boss))
                continue;

            var now = metadata.EntityPaused
                ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata)
                : _timing.CurTime;
            var duration = (bar.EndTime - bar.StartTime).TotalSeconds;
            var ratio = (float)Math.Clamp((now - bar.StartTime).TotalSeconds / duration, 0d, 1d); // cast progress from the server clock

            var pixelToScreen = Matrix3x2.CreateScale(1f / EyeManager.PixelsPerMeter,
                -1f / EyeManager.PixelsPerMeter) * matrix * worldToScreen;
            handle.SetTransform(pixelToScreen);
            var screenBounds = UIBox2.FromDimensions(new Vector2(bar.Offset.X - bar.Size.X / 2f,
                -bar.Offset.Y - bar.Size.Y / 2f), bar.Size);
            DrawBar(handle, screenBounds, bar, ratio);
            DrawLabel(handle, bar, label, pixelToScreen);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawBar(DrawingHandleScreen handle, UIBox2 bounds, BossCastBarComponent bar, float ratio)
    {
        var frame = GetTexture(bar.FrameTexture);
        var border = float.IsFinite(bar.BorderWidth)
            ? Math.Clamp(bar.BorderWidth, 0f, Math.Min(bounds.Width, bounds.Height) / 2f)
            : 0f;
        if (frame == null && border > 0f)
        {

            handle.DrawRect(new UIBox2(bounds.Left, bounds.Top, bounds.Right, bounds.Top + border), bar.BorderColor);
            handle.DrawRect(new UIBox2(bounds.Left, bounds.Bottom - border, bounds.Right, bounds.Bottom), bar.BorderColor);
            handle.DrawRect(new UIBox2(bounds.Left, bounds.Top + border, bounds.Left + border, bounds.Bottom - border), bar.BorderColor);
            handle.DrawRect(new UIBox2(bounds.Right - border, bounds.Top + border, bounds.Right, bounds.Bottom - border), bar.BorderColor);
        }
        var inner = new UIBox2(bounds.Left + border, bounds.Top + border, bounds.Right - border, bounds.Bottom - border);
        if (inner.Width > 0f && inner.Height > 0f)
        {
            if (GetTexture(bar.BackgroundTexture) is { } background)
                handle.DrawTextureRect(background, inner);
            else
                handle.DrawRect(inner, bar.BackgroundColor);

            if (ratio > 0f)
            {
                var fillBounds = UIBox2.FromDimensions(inner.TopLeft, new Vector2(inner.Width * ratio, inner.Height));
                if (GetTexture(bar.FillTexture) is { } fill)
                {
                    var region = new UIBox2(0f, 0f, fill.Width * ratio, fill.Height);
                    handle.DrawTextureRectRegion(fill, fillBounds, subRegion: region);
                }
                else
                {
                    handle.DrawRect(fillBounds, bar.FillColor);
                }
            }
        }

        if (frame != null)
            handle.DrawTextureRect(frame, bounds);
    }

    private void DrawLabel(DrawingHandleScreen handle, BossCastBarComponent bar, string text,
        Matrix3x2 pixelToScreen)
    {
        var top = -bar.Offset.Y + bar.Size.Y / 2f + TextGap;
        handle.SetTransform(Matrix3x2.CreateScale(TextScale) * pixelToScreen);
        DrawText(handle, new Vector2(bar.Offset.X - bar.Size.X / 2f + LabelPadding,
            top + LabelPadding) / TextScale, text, bar);
    }

    private void DrawText(DrawingHandleScreen handle, Vector2 position, string text, BossCastBarComponent bar)
    {
        handle.DrawString(_font, position + new Vector2(0.5f / TextScale), text, FontScale, bar.TextShadowColor);
        handle.DrawString(_font, position, text, FontScale, bar.TextColor);
    }

    private float MeasureText(string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
            width += _font.GetCharMetrics(rune, FontScale)?.Advance ?? 0;
        return width * TextScale;
    }

    private Texture? GetTexture(ResPath? path)
    {
        return path is { } resourcePath && _resources.TryGetResource<TextureResource>(resourcePath, out var resource)
            ? resource.Texture
            : null;
    }
}
