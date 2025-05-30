using Robust.Client.GameObjects;
using Content.Shared.Imperial.Medieval.Bonfire;

namespace Content.Client.Imperial.Medieval.Bonfire;

public sealed class BonfireSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BonfireComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }
    private void OnAppearanceChange(EntityUid uid, BonfireComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, BonfireVisualLayers.Fire, out var isFireVisible, args.Component))
        {
            args.Sprite.LayerSetVisible(BonfireVisualLayers.Fire, isFireVisible);
        }
    }
}
