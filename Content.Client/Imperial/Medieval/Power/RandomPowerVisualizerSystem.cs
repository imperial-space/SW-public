using Content.Shared.Imperial.Medieval.Power;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Power;

public sealed class RandomPowerVisualizerSystem : VisualizerSystem<RandomPowerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, RandomPowerComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, RandomPowerVisuals.Voltage, out var state, args.Component))
            return;

        SpriteSystem.LayerSetRsiState((uid, args.Sprite), RandomPowerVisuals.Voltage, state);
    }
}
