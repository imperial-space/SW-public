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

        SubscribeLocalEvent<MetaDataComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MetaDataComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(EntityUid uid, MetaDataComponent component, ComponentStartup args)
    {
        if (!IsBerryBushPrototype(component.EntityPrototype?.ID))
            return;

        if (!TryComp<AppearanceComponent>(uid, out var appearance) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        UpdateBerryBushSprite(uid, component.EntityPrototype?.ID, sprite, appearance);
    }

    private void OnAppearanceChange(EntityUid uid, MetaDataComponent component, ref AppearanceChangeEvent args)
    {
        if (!IsBerryBushPrototype(component.EntityPrototype?.ID) || args.Sprite == null)
            return;

        UpdateBerryBushSprite(uid, component.EntityPrototype?.ID, args.Sprite, args.Component);
    }

    private void UpdateBerryBushSprite(EntityUid uid, string? prototypeId, SpriteComponent sprite, AppearanceComponent appearance)
    {
        if (!_appearance.TryGetData(uid, MedievalBerryBushVisuals.HasBerries, out bool hasBerries, appearance))
            return;

        if (!TryGetBerriesSpritePath(prototypeId, out var berriesSpritePath))
            return;

        if (!TryGetBaseBushState(uid, sprite, out var baseState))
            return;

        var berriesStateName = $"{baseState.Name}-berries";
        var berriesState = new RSI.StateId(berriesStateName);
        var berriesRsiPath = new ResPath(berriesSpritePath);
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

    private static bool IsBerryBushPrototype(string? prototypeId)
    {
        return prototypeId is "MedievalGrassBush" or "MedievalGrassBushAutumn" or "MedievalGrassBushWinter";
    }

    private static bool TryGetBerriesSpritePath(string? prototypeId, out string berriesSpritePath)
    {
        switch (prototypeId)
        {
            case "MedievalGrassBush":
                berriesSpritePath = "/Textures/Imperial/Medieval/Decor/GrassBush.rsi";
                return true;
            case "MedievalGrassBushAutumn":
                berriesSpritePath = "/Textures/Imperial/Medieval/Decor/GrassBush_autumn.rsi";
                return true;
            case "MedievalGrassBushWinter":
                berriesSpritePath = "/Textures/Imperial/Medieval/Decor/GrassBush_winter.rsi";
                return true;
            default:
                berriesSpritePath = string.Empty;
                return false;
        }
    }
}
