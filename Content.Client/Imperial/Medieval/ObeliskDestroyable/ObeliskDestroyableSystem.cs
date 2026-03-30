using Content.Shared.Imperial.Medieval.ObeliskDestroyable;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.ObeliskDestroyable;

public sealed class ObeliskDestroyableSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObeliskDestroyableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ObeliskDestroyableComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<ObeliskDestroyableComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, ObeliskDestroyableComponent component, ComponentStartup args)
    {
        UpdateVisuals(uid, component);
    }

    private void OnAfterAutoHandleState(EntityUid uid, ObeliskDestroyableComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(uid, component);
    }

    private void OnShutdown(EntityUid uid, ObeliskDestroyableComponent component, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.RemoveLayer((uid, sprite), "outer", false);
    }

    private void UpdateVisuals(EntityUid uid, ObeliskDestroyableComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        UpdateBaseLayer(component, sprite);
        UpdateOuterLayer(uid, component, sprite);
    }

    private void UpdateBaseLayer(ObeliskDestroyableComponent component, SpriteComponent sprite)
    {
        if (component.BaseLayerStates.Count == 0)
            return;

        var phaseIndex = Math.Clamp(component.CurrentPhase, 0, component.BaseLayerStates.Count - 1);
        var state = component.BaseLayerStates[phaseIndex];
        if (string.IsNullOrWhiteSpace(state))
            return;

        sprite.LayerSetState("base", state);
    }

    private void UpdateOuterLayer(EntityUid uid, ObeliskDestroyableComponent component, SpriteComponent sprite)
    {
        var spritePath = sprite.BaseRSI?.Path;
        if (spritePath == null)
            return;

        var state = component.InvincibilityActive
            ? component.InvincibleOuterLayerState
            : component.OuterLayerState;

        if (string.IsNullOrWhiteSpace(state))
            return;

        EnsureOuterLayer(uid, sprite, state, spritePath.Value);
        if (!_sprite.LayerMapTryGet((uid, sprite), "outer", out _, false))
            return;

        sprite.LayerSetState("outer", state, spritePath.Value);
    }

    private void EnsureOuterLayer(EntityUid uid, SpriteComponent sprite, string state, ResPath spritePath)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), "outer", out _, false))
            return;

        var layer = _sprite.AddLayer((uid, sprite), new SpriteSpecifier.Rsi(spritePath, state), 1);
        _sprite.LayerMapSet((uid, sprite), "outer", layer);
    }
}
