using Content.Shared.MeleeParry.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Parry;

public sealed class MeleeParryEffectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<MeleeParryEffectComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var effect, out var sprite))
        {
            if (!effect.PhaseControlled)
                continue;

            if (!_sprite.TryGetLayer((uid, sprite), 0, out var layer, false) ||
                layer.ActualRsi is not { } rsi ||
                !rsi.TryGetState(layer.State, out var state))
            {
                continue;
            }

            var elapsed = Math.Max(0f, (float) (_timing.CurTime - effect.AnimationStartTime).TotalSeconds);
            _sprite.LayerSetAutoAnimated(layer, false);

            if (elapsed >= state.TotalDelay)
            {
                _sprite.LayerSetVisible(layer, false);
                continue;
            }

            _sprite.LayerSetAnimationTime(layer, elapsed);
        }
    }
}
