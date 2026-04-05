using Content.Shared.Imperial.DarkMage.Follower;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.DarkMage;

public sealed partial class MedievalFollowerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalFollowerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalFollowerComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnStartup(EntityUid uid, MedievalFollowerComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var spriteComponent))
            return;
        component.Layer = _spriteSystem.AddLayer((uid, spriteComponent), component.Sprite);
        _spriteSystem.LayerMapSet((uid, spriteComponent), Flame.Key, component.Layer);
    }
    private void OnComponentShutdown(EntityUid uid, MedievalFollowerComponent component, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var spriteComponent))
            return;
        _spriteSystem.RemoveLayer((uid, spriteComponent), component.Layer);
    }
    private enum Flame
    {
        Key,
    }
}

