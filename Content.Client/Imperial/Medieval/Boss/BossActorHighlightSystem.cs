using System.Linq;
using Content.Client.Outline;
using Content.Client.Viewport;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossActorHighlightSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> OutlinePrototype = "SelectionOutlineInrange";
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    private readonly Dictionary<EntityUid, OutlineState> _highlighted = new();

    private sealed class OutlineState
    {
        public required ShaderInstance Shader;
        public ShaderInstance? Previous;
        public Color Color;
        public int RenderScale;
    }

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(InteractionOutlineSystem));
        UpdatesAfter.Add(typeof(TargetOutlineSystem));
    }

    public override void Shutdown()
    {
        foreach (var target in _highlighted.Keys.ToArray())
            Clear(target);
        base.Shutdown();
    }
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var desired = new Dictionary<EntityUid, Color>();
        var query = EntityManager.AllEntityQueryEnumerator<BossActorMarkerComponent>();
        while (query.MoveNext(out _, out var mark))
        {
            if (mark.Kind is not (BossActorMarkerKind.Spread or BossActorMarkerKind.Stack or BossActorMarkerKind.TankBuster) ||
                !TryComp<TransformComponent>(mark.Target, out var xform) || xform.MapUid is not { } map ||
                !HasComp<BossArenaRulesComponent>(map) ||
                !TryComp<TransformComponent>(mark.Boss, out var bossXform) || bossXform.MapUid != map ||
                !TryComp<SpriteComponent>(mark.Target, out var sprite) || !sprite.Visible || sprite.ContainerOccluded ||
                !_prototypes.TryIndex(mark.Style, out var style))
                continue;

            if (BossSpreadSoulVisuals.ReplacesLegacyCue(style, mark.Kind) || BossCommunionVisuals.ReplacesLegacyCue(style, mark.Kind))
                continue;

            var color = (mark.Kind switch
            {
                BossActorMarkerKind.Spread => style.SpreadHighlightColor,
                BossActorMarkerKind.TankBuster => style.TankBusterColor,
                _ => style.StackHighlightColor
            }).WithAlpha(0.8f);
            if (mark.Kind == BossActorMarkerKind.TankBuster || !desired.ContainsKey(mark.Target))
                desired[mark.Target] = color;
        }

        foreach (var target in _highlighted.Keys.ToArray())
            if (!desired.ContainsKey(target))
                Clear(target);

        if (desired.Count == 0) return;
        var renderScale = _eye.MainViewport.GetRenderScale();
        foreach (var (target, color) in desired)
        {
            var sprite = Comp<SpriteComponent>(target);
            if (!_highlighted.TryGetValue(target, out var state))
            {
                state = new OutlineState { Shader = _prototypes.Index(OutlinePrototype).InstanceUnique() };
                state.Shader.SetParameter("outline_fullbright", true);
                _highlighted[target] = state;
            }
            if (state.Color != color)
            {
                state.Shader.SetParameter("outline_color", color);
                state.Color = color;
            }
            if (state.RenderScale != renderScale)
            {
                state.Shader.SetParameter("outline_width", (float)renderScale);
                state.RenderScale = renderScale;
            }
            if (sprite.PostShader != state.Shader)
            {
                state.Previous = sprite.PostShader;
                sprite.PostShader = state.Shader;
            }
        }
    }

    private void Clear(EntityUid target)
    {
        if (!_highlighted.Remove(target, out var state)) return;
        if (TryComp<SpriteComponent>(target, out var sprite) && sprite.PostShader == state.Shader)
            sprite.PostShader = state.Previous is { Disposed: false } previous ? previous : null;
        state.Shader.Dispose();
    }
}
