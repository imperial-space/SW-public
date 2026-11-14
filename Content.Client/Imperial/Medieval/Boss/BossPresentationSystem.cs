using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossPresentationSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    private readonly Dictionary<EntityUid, Vector2> _offsets = new();
    private readonly Dictionary<EntityUid, Color> _colors = new();
    private readonly HashSet<EntityUid> _bodyApplied = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossPresentationComponent, ComponentShutdown>(OnPresentationShutdown);
        _overlays.AddOverlay(new BossCastingEffectOverlay(EntityManager, _resources, _prototypes, _timing));
    }

    public override void Shutdown()
    {
        foreach (var (uid, offset) in _offsets)
            if (TryComp<SpriteComponent>(uid, out var sprite)) _sprites.SetOffset((uid, sprite), offset);
        _offsets.Clear();
        foreach (var (uid, color) in _colors)
            if (TryComp<SpriteComponent>(uid, out var sprite)) _sprites.SetColor((uid, sprite), color);
        _colors.Clear();
        _bodyApplied.Clear();
        _overlays.RemoveOverlay<BossCastingEffectOverlay>();
        base.Shutdown();
    }
    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<BossPresentationComponent, SpriteComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var presentation, out var sprite, out var metadata))
        {
            if (presentation.Body is { } body && _bodyApplied.Add(uid))
                _sprites.LayerSetSprite((uid, sprite), 0, new SpriteSpecifier.Rsi(
                    Robust.Shared.Serialization.TypeSerializers.Implementations.SpriteSpecifierSerializer.TextureRoot / body.RsiPath, body.RsiState));
            if (!_offsets.TryGetValue(uid, out var offset)) _offsets[uid] = offset = sprite.Offset;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            _sprites.SetOffset((uid, sprite), offset + new Vector2(0, BossPresentationComponent.BobOffset(now * presentation.BobSpeedMultiplier)));
            if (TryComp<BossVigilComponent>(uid, out var vigil))
            {
                if (!_colors.TryGetValue(uid, out var color)) _colors[uid] = color = sprite.Color;
                var opacity = BossVigilVisuals.BodyOpacity(vigil, now);
                _sprites.SetColor((uid, sprite), color.WithAlpha(color.A * opacity));
                _sprites.SetVisible((uid, sprite), opacity > 0);
            }
            else
            {
                if (_colors.Remove(uid, out var color)) _sprites.SetColor((uid, sprite), color);
                _sprites.SetVisible((uid, sprite), !presentation.Hidden);
            }
        }
    }

    private void OnPresentationShutdown(EntityUid uid, BossPresentationComponent component, ComponentShutdown args)
    {
        _bodyApplied.Remove(uid);
        if (_offsets.Remove(uid, out var offset) && !Terminating(uid) && TryComp<SpriteComponent>(uid, out var sprite))
            _sprites.SetOffset((uid, sprite), offset);
        if (_colors.Remove(uid, out var color) && !Terminating(uid) && TryComp<SpriteComponent>(uid, out var colored))
            _sprites.SetColor((uid, colored), color);
    }
}

public sealed class BossCastingEffectOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Unshaded = "unshaded";
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly SpriteSystem _sprites;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public BossCastingEffectOverlay(IEntityManager entities, IResourceCache resources,
        IPrototypeManager prototypes, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _sprites = entities.System<SpriteSystem>();
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index(Unshaded).Instance();
        ZIndex = BossOverlayLayers.Ground;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var query = _entities.AllEntityQueryEnumerator<BossPresentationComponent, BossCastBarComponent, TransformComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var presentation, out var bar, out var xform))
        {
            if (presentation.Hidden || !bar.Casting || !bar.Visible || bar.EndTime <= bar.StartTime ||
                xform.MapID != args.MapId || xform.MapUid is not { } map || !_entities.HasComponent<BossArenaRulesComponent>(map)) continue;
            var specifier = presentation.GetCastEffect(bar.Preparing);
            if (specifier == null) continue;
            var rsi = _resources.GetResource<RSIResource>(specifier.RsiPath).RSI;
            var now = _entities.GetComponent<MetaDataComponent>(uid).EntityPaused
                ? _timing.CurTime - _metadata.GetPauseTime(uid) : _timing.CurTime;
            var progress = Math.Clamp((now - bar.StartTime).TotalSeconds / (bar.EndTime - bar.StartTime).TotalSeconds, 0, 1);
            var frame = _sprites.GetFrame(specifier, TimeSpan.FromSeconds(progress * rsi[specifier.RsiState].TotalDelay), loop: false);
            var size = frame.Size / (float)EyeManager.PixelsPerMeter * presentation.CastEffectScale;
            handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(_transform.GetWorldPosition(xform)));
            handle.DrawTextureRect(frame, Box2.FromDimensions(-size / 2, size),
                bar.Preparing ? presentation.PreparedCastEffectColor : presentation.CastEffectColor);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
