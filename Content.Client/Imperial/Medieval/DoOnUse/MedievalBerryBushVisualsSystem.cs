using Content.Shared.Imperial.Medieval.DoOnUse.DoAfter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.DoOnUse;

public sealed class MedievalBerryBushVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

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

        if (!TryGetSpritePaths(prototypeId, out var berriesSpritePath, out var emptySpritePath))
            return;

        var targetRsi = hasBerries ? berriesSpritePath : emptySpritePath;
        var state = _sprite.LayerGetRsiState((uid, sprite), 0);
        if (!state.IsValid)
            state = new RSI.StateId("grass_high_bush1");

        _sprite.LayerSetRsi((uid, sprite), 0, new ResPath(targetRsi), state);
    }

    private static bool IsBerryBushPrototype(string? prototypeId)
    {
        return prototypeId is "MedievalGrassBush" or "MedievalGrassBushAutumn" or "MedievalGrassBushWinter";
    }

    private static bool TryGetSpritePaths(string? prototypeId, out string berriesSpritePath, out string emptySpritePath)
    {
        switch (prototypeId)
        {
            case "MedievalGrassBush":
                berriesSpritePath = "/Textures/Imperial/Medieval/Decor/GrassBush.rsi";
                emptySpritePath = "/Textures/Imperial/Medieval/Decor/GrassHighBush.rsi";
                return true;
            case "MedievalGrassBushAutumn":
                berriesSpritePath = "/Textures/Imperial/Medieval/Decor/GrassBush_autumn.rsi";
                emptySpritePath = "/Textures/Imperial/Medieval/Decor/GrassHighBush_autumn.rsi";
                return true;
            case "MedievalGrassBushWinter":
                berriesSpritePath = "/Textures/Imperial/Medieval/Decor/GrassBush_winter.rsi";
                emptySpritePath = "/Textures/Imperial/Medieval/Decor/GrassHighBush.rsi";
                return true;
            default:
                berriesSpritePath = string.Empty;
                emptySpritePath = string.Empty;
                return false;
        }
    }
}
