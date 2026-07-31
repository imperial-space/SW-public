using Content.Shared.Imperial.Medieval.DoOnUse.DoAfter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.DoOnUse;

public sealed class MedievalBerryBushVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string BerriesLayerKey = "berries";
    private const string BushRandomLayerKey = "random";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalBerryBushComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalBerryBushComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(EntityUid uid, MedievalBerryBushComponent component, ComponentStartup args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        UpdateBerryBushSprite(uid, component, sprite, appearance);
    }

    private void OnAppearanceChange(EntityUid uid, MedievalBerryBushComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateBerryBushSprite(uid, component, args.Sprite, args.Component);
    }

    private void UpdateBerryBushSprite(EntityUid uid, MedievalBerryBushComponent component, SpriteComponent sprite, AppearanceComponent appearance)
    {
        if (!_appearance.TryGetData(uid, MedievalBerryBushVisuals.HasBerries, out bool hasBerries, appearance))
            return;

        var baseRsi = _sprite.LayerGetEffectiveRsi((uid, sprite), 0);
        if (baseRsi == null)
            return;

        var berriesRsiPath = baseRsi.Path;

        if (!TryGetBaseBushState(uid, sprite, out var baseState))
            return;

        var berriesStateName = $"{baseState.Name}-berries";
        var berriesState = new RSI.StateId(berriesStateName);
        if (!_resourceCache.TryGetResource(berriesRsiPath, out RSIResource? resource)
            || !resource.RSI.TryGetState(berriesState, out _))
        {
            if (_sprite.LayerMapTryGet((uid, sprite), BerriesLayerKey, out var hiddenLayer, false))
                _sprite.LayerSetVisible((uid, sprite), hiddenLayer, false);

            return;
        }

        if (!_sprite.LayerMapTryGet((uid, sprite), BerriesLayerKey, out var berriesLayer, false))
        {
            berriesLayer = _sprite.AddLayer((uid, sprite), new SpriteSpecifier.Rsi(berriesRsiPath, berriesStateName));
            _sprite.LayerMapSet((uid, sprite), BerriesLayerKey, berriesLayer);
        }
        else
        {
            _sprite.LayerSetRsi((uid, sprite), berriesLayer, berriesRsiPath, berriesState);
        }

        _sprite.LayerSetVisible((uid, sprite), berriesLayer, hasBerries);
    }

    private bool TryGetBaseBushState(EntityUid uid, SpriteComponent sprite, out RSI.StateId state)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), BushRandomLayerKey, out var baseLayer, false))
        {
            state = _sprite.LayerGetRsiState((uid, sprite), baseLayer);
            return state.IsValid;
        }

        state = _sprite.LayerGetRsiState((uid, sprite), 0);
        return state.IsValid;
    }

}
