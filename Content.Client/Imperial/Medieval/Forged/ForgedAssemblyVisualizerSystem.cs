using Content.Shared.Forged;
using Content.Shared.Imperial.Medieval.Forged;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Forged;

public sealed class ForgedAssemblyVisualizerSystem : VisualizerSystem<ForgedAssemblyComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, ForgedAssemblyComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null) return;

        UpdateAppearance(uid, args.Sprite, args.Component);
    }

    private void UpdateAppearance(EntityUid uid, SpriteComponent sprite, AppearanceComponent appearance)
    {
        foreach (ForgedVisuals visualKey in Enum.GetValues(typeof(ForgedVisuals)))
        {
            string key = visualKey.ToString();
            if (AppearanceSystem.TryGetData<ForgedVisualsPacket>(uid, visualKey, out var packet, appearance)) SetLayer(uid, key, packet, sprite);
        }
    }

    private void SetLayer(EntityUid uid, string layerName, ForgedVisualsPacket packet, SpriteComponent sprite)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), layerName, out var index, false))
        {
            _sprite.LayerSetRsi((uid, sprite), index, packet.RsiPath);
            _sprite.LayerSetRsiState((uid, sprite), index, packet.State);
            _sprite.LayerSetVisible((uid, sprite), index, true);
        }
    }
}
