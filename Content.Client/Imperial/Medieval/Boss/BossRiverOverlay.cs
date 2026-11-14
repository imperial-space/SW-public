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

public sealed class BossRiverOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    public BossRiverOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();

        ZIndex = BossOverlayLayers.Ground;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var impact, out var transform, out var metadata))
        {
            if (!impact.Impact || impact.TankTarget != null || transform.MapID != args.MapId ||
                !_entities.EntityExists(impact.Grid) || !_entities.EntityExists(impact.Boss) ||
                !_prototypes.TryIndex(impact.Style, out var style)) continue;
            var gridMatrix = _transform.GetWorldMatrix(impact.Grid);
            var cardinal = BossArenaWarningVisuals.ProfileRotation(
                _transform.GetWorldRotation(impact.Grid) + (args.Viewport.Eye?.Rotation ?? Angle.Zero));
            var turns = (int)Math.Round(cardinal.Degrees / 90);
            if (BossRiverVisuals.Texture(style, impact.Pattern, BossTilePatterns.Rotate(impact.PatternOrigin, turns),
                    impact.HalfSize, (impact.Facing + turns) % 4) is not { } path ||
                !_resources.TryGetResource<RSIResource>(path, out var resource) ||
                !resource.RSI.TryGetState(BossRiverVisuals.State, out var state)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            if (now < impact.StartTime || now >= impact.EndTime) continue;
            var index = Math.Min(BossRiverVisuals.FrameIndex((now - impact.StartTime).TotalSeconds), state.DelayCount - 1); // choose the frame from the shared impact time
            var frame = state.GetFrame(RsiDirection.South, index);

            handle.SetTransform(Matrix3x2.CreateRotation(-(float)cardinal) *
                Matrix3x2.CreateTranslation(new Vector2(0.5f)) * gridMatrix);

            handle.DrawTextureRect(frame, BossRiverVisuals.TextureBounds(frame.Size), Color.White);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class BossRiverOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossRiverOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
