using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Weapons.Marker;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class CursedMarkVisualizerSystem : SharedDamageMarkerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CursedMarkComponent, ComponentStartup>(OnMarkerStartup);
        SubscribeLocalEvent<CursedMarkComponent, ComponentShutdown>(OnMarkerShutdown);
    }

    private void OnMarkerStartup(EntityUid uid, CursedMarkComponent component, ComponentStartup args)
    {
        if (!_timing.ApplyingState || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var layer = _sprite.LayerMapReserve((uid, sprite), DamageMarkerKey.Key);
        _sprite.LayerSetRsi((uid, sprite), layer, new ResPath("Textures/Imperial/Medieval/Effects/cursed-mark.rsi"), "mark");
    }

    private void OnMarkerShutdown(EntityUid uid, CursedMarkComponent component, ComponentShutdown args)
    {
        if (!_timing.ApplyingState || !TryComp<SpriteComponent>(uid, out var sprite) || !_sprite.LayerMapTryGet((uid, sprite), DamageMarkerKey.Key, out var weh, false))
            return;

        _sprite.RemoveLayer((uid, sprite), weh);
    }

    private enum DamageMarkerKey : byte
    {
        Key
    }
}
