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
        foreach (ForgedVisuals visualKey in Enum.GetValues(typeof(ForgedVisuals)))
        {
            string key = visualKey.ToString();
            if (AppearanceSystem.TryGetData<string>(uid, visualKey, out var state, appearance)) SetLayer(uid, key, state, sprite);
        }
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
