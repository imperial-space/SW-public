using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Ships.Anchor;

public sealed class ClientAnchorSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, MedievalAnchorComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<bool>(uid, MedievalAnchorVisuals.Enabled, out var enabled, args.Component))
            enabled = component.Enabled;

        var state = enabled ? component.DownState : component.UpState;
        _sprite.LayerSetRsiState((uid, args.Sprite), MedievalAnchorVisualLayers.Base, state);
    }
}
