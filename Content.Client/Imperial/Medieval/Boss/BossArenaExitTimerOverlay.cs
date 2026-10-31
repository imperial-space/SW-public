using System.Collections.Generic;
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

public sealed class BossArenaExitTimerOverlay : Overlay
{
    private const float TextScale = 7f / 32f;
    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly ExamineSystemShared _examine;
    private readonly Font _font;
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public BossArenaExitTimerOverlay(IEntityManager entities, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _examine = entities.System<ExamineSystemShared>();
        _font = new VectorFont(resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 32);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null) return;
        var handle = args.ScreenHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var worldToScreen = args.ViewportControl.GetWorldToScreenMatrix();
        var query = _entities.AllEntityQueryEnumerator<BossArenaExitTimerComponent, SpriteComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var timer, out var sprite, out var xform, out var metadata))
        {
            if (timer.EndTime <= TimeSpan.Zero || xform.MapID != args.MapId || !sprite.Visible || sprite.ContainerOccluded ||
                (metadata.Flags & MetaDataFlags.Detached) != 0) continue;
            var position = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Contains(position)) continue;
            if (args.Viewport.Eye is { DrawFov: true } eye && !_examine.InRangeUnOccluded(
                    eye.Position, new MapCoordinates(position, args.MapId), 0f, uid,
                    static (candidate, flame) => candidate == flame)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            var text = timer.GetLocalizedRemainingTime(now);
            var width = 0f;
            foreach (var rune in text.EnumerateRunes()) width += _font.GetCharMetrics(rune, 1)?.Advance ?? 0;
            var matrix = rotation * Matrix3Helpers.CreateTranslation(position);
            var pixels = Matrix3x2.CreateScale(1f / EyeManager.PixelsPerMeter, -1f / EyeManager.PixelsPerMeter)
                * matrix * worldToScreen;
            handle.SetTransform(Matrix3x2.CreateScale(TextScale) * pixels);
            var label = new Vector2(timer.Offset.X / TextScale - width / 2f, -timer.Offset.Y / TextScale);
            handle.DrawString(_font, label + new Vector2(0.5f / TextScale), text, 1, Color.Black);
            handle.DrawString(_font, label, text, 1, Color.White);
        }
        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class BossArenaExitTimerSystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossArenaExitTimerOverlay(EntityManager, Resources, Timing);
    }
}
