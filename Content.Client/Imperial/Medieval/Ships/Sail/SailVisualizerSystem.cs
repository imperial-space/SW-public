using Content.Shared.Imperial.Medieval.Ships.Sail;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Ships.Sail;

public sealed class SailVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SailComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, SailComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<bool>(uid, SailVisuals.Folded, out var folded, args.Component))
            return;

        _sprite.LayerSetVisible((uid, args.Sprite), SailVisualLayers.Unfolded, !folded);
        _sprite.LayerSetVisible((uid, args.Sprite), SailVisualLayers.Folded, folded);
    }
}
