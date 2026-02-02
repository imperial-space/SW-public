using Content.Shared.Forged;
using Content.Shared.Imperial.Medieval.Forged;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Forged;

public sealed class ForgedVisualizerSystem : VisualizerSystem<ForgedAssemblyComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, ForgedAssemblyComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null) return;

        UpdateAppearance(uid, component, args.Sprite, args.Component);
    }

    private void UpdateAppearance(EntityUid uid, ForgedAssemblyComponent component, SpriteComponent sprite, AppearanceComponent appearance)
    {
        if (AppearanceSystem.TryGetData<string>(uid, ForgedVisuals.head, out var headState, appearance)) SetLayer(uid, "head", headState, sprite);
        if (AppearanceSystem.TryGetData<string>(uid, ForgedVisuals.r_arm, out var r_armState, appearance)) SetLayer(uid, "r_arm", r_armState, sprite);
        if (AppearanceSystem.TryGetData<string>(uid, ForgedVisuals.l_arm, out var l_armState, appearance)) SetLayer(uid, "l_arm", l_armState, sprite);
        if (AppearanceSystem.TryGetData<string>(uid, ForgedVisuals.core, out var coreState, appearance)) SetLayer(uid, "core", coreState, sprite);
        if (AppearanceSystem.TryGetData<string>(uid, ForgedVisuals.legs, out var legsState, appearance)) SetLayer(uid, "legs", legsState, sprite);
    }

    private void SetLayer(EntityUid uid, string layerName, string state, SpriteComponent sprite)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), layerName, out var index, false))
        {
            _sprite.LayerSetRsiState((uid, sprite), index, state);
            _sprite.LayerSetVisible((uid, sprite), index, true);
        }
    }
}
