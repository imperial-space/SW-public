using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossBrazierAnimationSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    private static readonly string[] FlameLayers = ["white", "purple", "victory"];

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var query = AllEntityQuery<BossBrazierAnimationComponent, SpriteComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var animation, out var sprite, out var metadata))
        {
            var now = _timing.CurTime - _metadata.GetPauseTime(uid, metadata);   // keep the flame still while paused
            var time = BossBrazierAnimation.TimeAt(animation, now);
            foreach (var key in FlameLayers)
            {
                if (!_sprites.LayerMapTryGet((uid, sprite), key, out var layer, false)) continue;
                _sprites.LayerSetAutoAnimated((uid, sprite), layer, false);
                _sprites.LayerSetAnimationTime((uid, sprite), layer, time);
            }
        }
    }
}
