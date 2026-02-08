using Content.Shared.Forged;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Forged;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Forged;

public sealed class ForgedVisualizerSystem : VisualizerSystem<ForgedComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, ForgedComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null) return;

        UpdateAppearance(uid, component, args.Sprite, args.Component);
    }

    private void UpdateAppearance(EntityUid uid, ForgedComponent component, SpriteComponent sprite, AppearanceComponent appearance)
    {
        foreach (ForgedAssemblyVisuals visualKey in Enum.GetValues(typeof(ForgedAssemblyVisuals)))
        {
            if (AppearanceSystem.TryGetData<ForgedVisualsPacket>(uid, visualKey, out var packet, appearance)) SetLayer(uid, visualKey, packet, sprite);
        }

        _sprite.LayerSetVisible((uid, sprite), HumanoidVisualLayers.LFoot, false);
        _sprite.LayerSetVisible((uid, sprite), HumanoidVisualLayers.RFoot, false);
    }

    private void SetLayer(EntityUid uid, ForgedAssemblyVisuals partKey, ForgedVisualsPacket packet, SpriteComponent sprite)
    {
        if (partKey != ForgedAssemblyVisuals.core)
        {
            HumanoidVisualLayers? targetLayer = partKey switch
            {
                ForgedAssemblyVisuals.head       => HumanoidVisualLayers.Head,
                ForgedAssemblyVisuals.torso      => HumanoidVisualLayers.Chest, // Torso в коде обычно мапится на Chest
                ForgedAssemblyVisuals.right_arm  => HumanoidVisualLayers.RArm,
                ForgedAssemblyVisuals.left_arm   => HumanoidVisualLayers.LArm,
                ForgedAssemblyVisuals.right_hand => HumanoidVisualLayers.RHand,
                ForgedAssemblyVisuals.left_hand  => HumanoidVisualLayers.LHand,
                ForgedAssemblyVisuals.right_leg  => HumanoidVisualLayers.RLeg,
                ForgedAssemblyVisuals.left_leg   => HumanoidVisualLayers.LLeg,

                // Core (Ядро) обычно не имеет внешнего визуального слоя на кукле человека,
                // поэтому возвращаем null или дефолтное значение.
                ForgedAssemblyVisuals.core       => null,

                _ => null // Для любых непредусмотренных случаев
            };

            if (targetLayer == null) return;

            if (_sprite.LayerMapTryGet((uid, sprite), targetLayer.Value, out var index, false))
            {
                _sprite.LayerSetRsiState((uid, sprite), index, packet.State);
                _sprite.LayerSetRsi((uid, sprite), index, packet.RsiPath);
                _sprite.LayerSetVisible((uid, sprite), index, true);
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet((uid, sprite), partKey.ToString(), out var index, false))
            {
                _sprite.LayerSetRsiState((uid, sprite), index, packet.State);
                _sprite.LayerSetRsi((uid, sprite), index, packet.RsiPath);
                _sprite.LayerSetVisible((uid, sprite), index, true);
            }
        }
    }
}
