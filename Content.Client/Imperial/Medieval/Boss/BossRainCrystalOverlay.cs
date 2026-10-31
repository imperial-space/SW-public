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

public sealed class BossRainCrystalOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    public BossRainCrystalOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = 850;
    }

    private bool HasMatchingImpact(BossTelegraphComponent warning, TimeSpan now)
    {
        var impacts = _entities.AllEntityQueryEnumerator<BossTelegraphComponent>();
        while (impacts.MoveNext(out _, out var impact))
        {
            if (impact.Impact && impact.Boss == warning.Boss && impact.Grid == warning.Grid &&
                impact.Pattern == warning.Pattern && impact.PatternOrigin == warning.PatternOrigin &&
                impact.StartTime >= warning.EndTime && now < impact.EndTime)
                return true;
        }
        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var visual, out var transform, out var metadata))
        {
            if (!BossRainCrystalVisuals.IsCrystal(visual.Pattern) || visual.TankTarget != null ||
                transform.MapID != args.MapId || !_entities.EntityExists(visual.Grid) || !_entities.EntityExists(visual.Boss) ||
                !_prototypes.TryIndex(visual.Style, out var style)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            if (now < visual.StartTime || visual.Impact && now >= visual.EndTime) continue;
            var duration = (visual.EndTime - visual.StartTime).TotalSeconds;
            var playback = BossRainCrystalVisuals.Playback((now - visual.StartTime).TotalSeconds, duration, visual.Impact);

            if (playback.Impact && playback.Elapsed >= BossRainCrystalVisuals.ShatterSeconds) continue;
            if (!visual.Impact && playback.Impact && HasMatchingImpact(visual, now)) continue;

            if (!visual.Impact && now < visual.EndTime &&
                BossRainCrystalVisuals.Texture(style, visual.Pattern, true) is { } impactPath)
                _resources.TryGetResource<RSIResource>(impactPath, out _);
            if (BossRainCrystalVisuals.Texture(style, visual.Pattern, playback.Impact) is not { } path ||
                !_resources.TryGetResource<RSIResource>(path, out var resource) ||
                !resource.RSI.TryGetState(BossRainCrystalVisuals.State, out var state)) continue;
            var index = BossRainCrystalVisuals.FrameIndex(playback.Elapsed, duration, playback.Impact);
            var frame = state.GetFrame(RsiDirection.South, Math.Min(index, state.DelayCount - 1));
            var anchor = BossRainCrystalVisuals.Anchor(playback.Impact);
            var bottom = new Vector2(-anchor.X, anchor.Y - frame.Height) / BossRainCrystalVisuals.PixelsPerTile;
            var center = BossRainCrystalVisuals.Center(visual.Pattern, visual.PatternOrigin); // match the crystal to its warning footprint
            handle.SetTransform(Matrix3x2.CreateTranslation(center) * _transform.GetWorldMatrix(visual.Grid));
            handle.DrawTextureRect(frame, Box2.FromDimensions(bottom, frame.Size / BossRainCrystalVisuals.PixelsPerTile), Color.White);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class BossRainCrystalOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossRainCrystalOverlay(EntityManager, Prototypes, Resources, Timing);
    }
}
