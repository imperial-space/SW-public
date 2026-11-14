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

public class BossExecutionLineOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Unshaded = "unshaded";
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    protected readonly IEntityManager Entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    protected virtual bool Eyes => false;

    public BossExecutionLineOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        Entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index(Unshaded).Instance();
        ZIndex = BossOverlayLayers.Ground;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = Entities.AllEntityQueryEnumerator<BossExecutionLineComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var line, out var transform, out var metadata))
        {
            if (transform.MapID != args.MapId || !Entities.EntityExists(line.Grid) || !Entities.EntityExists(line.Boss) ||
                !_prototypes.TryIndex(line.Style, out var style)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            if (now < line.StartTime || now >= line.EndTime) continue;
            if (!line.Resolved && style.ExecutionEyesTexture is { } eyes &&
                Entities.TryGetComponent<TransformComponent>(line.Target, out var target) && target.MapID == args.MapId &&
                _resources.TryGetResource<RSIResource>(eyes, out var source) && source.RSI.TryGetState(Eyes ? "eyes" : "shadow", out var state))
            {
                var index = (int)((now - line.StartTime).TotalSeconds / BossExecutionLineVisuals.EyesFrameDuration) % state.DelayCount;
                var frame = state.GetFrame(RsiDirection.South, index);
                var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
                var position = _transform.GetWorldPosition(target) + Vector2.TransformNormal(BossExecutionLineVisuals.EyesOffset, rotation);
                handle.SetTransform(rotation * Matrix3x2.CreateTranslation(position));
                handle.DrawTextureRect(frame, BossExecutionLineVisuals.EyesBounds(frame.Size), Color.White);
            }
            if (Eyes) continue;
            var origin = line.Origin;
            var direction = line.Direction;
            var length = line.Length;
            if (!line.Resolved && Entities.TryGetComponent<TransformComponent>(line.Target, out var aim) && aim.MapID == args.MapId)
            {
                var inverse = _transform.GetInvWorldMatrix(line.Grid);
                origin = Vector2.Transform(_transform.GetWorldPosition(line.Boss), inverse);
                var targetPosition = Vector2.Transform(_transform.GetWorldPosition(aim), inverse);
                direction = BossExecutionLineVisuals.Aim(origin, targetPosition, direction);
                length = BossExecutionLineVisuals.LengthToEdge(origin, direction, line.HalfSize);
            }

            var visualLength = line.Resolved ? length :
                BossArenaWarningVisuals.LineLength(origin, direction, length, line.HalfSize, style.WarningOverflowTiles);
            handle.SetTransform(Matrix3x2.CreateRotation(MathF.Atan2(direction.Y, direction.X)) *
                Matrix3x2.CreateTranslation(origin) * _transform.GetWorldMatrix(line.Grid));
            if (!line.Resolved)
            {
                if (TryDrawWarning(handle, style, visualLength, line.Width, (now - line.StartTime).TotalSeconds))
                    continue;
                var color = Color.FromHex("#802E99");
                handle.DrawRect(Box2.FromDimensions(new Vector2(0, -line.Width / 2), new Vector2(visualLength, line.Width)), color.WithAlpha(.16f));
                handle.DrawLine(new Vector2(0, -line.Width / 2), new Vector2(visualLength, -line.Width / 2), color.WithAlpha(.65f));
                handle.DrawLine(new Vector2(0, line.Width / 2), new Vector2(visualLength, line.Width / 2), color.WithAlpha(.65f));
                handle.DrawLine(Vector2.Zero, new Vector2(visualLength, 0), color.WithAlpha(.35f));
                continue;
            }
            var age = (now - line.ImpactTime).TotalSeconds;
            var frameIndex = (int)Math.Floor(age / BossExecutionLineVisuals.FrameDuration + 1e-7);
            if (frameIndex < 0) continue;
            var visible = BossExecutionLineVisuals.VisibleLength(visualLength, age);
            TryDrawGroundSlash(handle, style, line.Grid, origin, direction, visible, frameIndex,
                (float)(args.Viewport.Eye?.Rotation ?? Angle.Zero));

        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private bool TryDrawWarning(DrawingHandleWorld handle, BossTelegraphStylePrototype style,
        float length, float width, double age)
    {
        if (style.ExecutionWarningTexture is not { } path ||
            !_resources.TryGetResource<RSIResource>(path, out var resource) ||
            !resource.RSI.TryGetState("warning", out var state))
            return false;
        var index = (int)Math.Floor(age / BossExecutionLineVisuals.WarningFrameDuration + 1e-7);
        if (index < 0) return false;
        index %= state.DelayCount;
        var texture = state.GetFrame(RsiDirection.South, index);
        var height = texture.Height / 32f;
        if (MathF.Abs(height - width) > .0001f) return false;
        DrawNativeStrip(handle, texture, length, BossExecutionLineVisuals.WarningOpacity);
        return true;
    }

    private static void DrawNativeStrip(DrawingHandleWorld handle, Texture texture, float length, float opacity)
    {
        var height = texture.Height / 32f;
        var span = texture.Width / 32f;

        for (var x = 0f; x < length; x += span)
        {
            var remaining = MathF.Min(span, length - x);
            handle.DrawTextureRectRegion(texture, Box2.FromDimensions(new Vector2(x, -height / 2), new Vector2(remaining, height)),
                Color.White.WithAlpha(opacity), UIBox2.FromDimensions(Vector2.Zero, new Vector2(remaining * 32, texture.Height)));
        }
    }

    private bool TryDrawGroundSlash(DrawingHandleWorld handle, BossTelegraphStylePrototype style,
        EntityUid grid, Vector2 origin, Vector2 direction, float visible, int frameIndex, float eyeRotation)
    {
        if (style.ExecutionTipTexture is not { } tipPath ||
            !_resources.TryGetResource<RSIResource>(tipPath, out var tipResource) ||
            !tipResource.RSI.TryGetState("strike", out var tip))
            return false;

        if (style.ExecutionTrailTexture is { } trailPath &&
            _resources.TryGetResource<RSIResource>(trailPath, out var trailResource) &&
            trailResource.RSI.TryGetState("strike", out var trail))
        {

            DrawNativeStrip(handle, trail.GetFrame(RsiDirection.South, Math.Min(frameIndex, trail.DelayCount - 1)), visible, 1);
        }
        else return false;

        var gridMatrix = _transform.GetWorldMatrix(grid);
        var worldDirection = Vector2.TransformNormal(direction, gridMatrix);
        var (facing, rotation) = BossExecutionLineVisuals.TipPose(worldDirection, eyeRotation);
        var (profile, mirror) = BossExecutionLineVisuals.TipProfile(facing);
        var texture = tip.GetFrame(profile, Math.Min(frameIndex, tip.DelayCount - 1));
        var endpoint = Vector2.Transform(origin + direction * visible, gridMatrix);
        handle.SetTransform(Matrix3x2.CreateScale(mirror ? -1 : 1, 1) *
            Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(endpoint));
        handle.DrawTextureRect(texture, BossExecutionLineVisuals.TipBounds(profile, texture.Size), Color.White);
        handle.SetTransform(Matrix3x2.CreateRotation(MathF.Atan2(direction.Y, direction.X)) *
            Matrix3x2.CreateTranslation(origin) * gridMatrix);
        return true;
    }
}

public sealed class BossExecutionEyesOverlay : BossExecutionLineOverlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    protected override bool Eyes => true;
    public BossExecutionEyesOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
        : base(entities, prototypes, resources, timing) => ZIndex = 1000;
}

public sealed class BossExecutionOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossExecutionLineOverlay(EntityManager, Prototypes, Resources, Timing);
        yield return new BossExecutionEyesOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
