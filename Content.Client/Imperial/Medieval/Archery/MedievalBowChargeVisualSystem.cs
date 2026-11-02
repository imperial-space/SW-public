
using Content.Shared.Imperial.Medieval.Archery;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Archery;

public sealed class MedievalBowChargeVisualSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private EntityUid? _readyBow;
    private TimeSpan _readyStart;
    private bool _readySoundPlayed;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<MedievalBowChargeComponent>();

        while (query.MoveNext(out var bow, out var charge))
        {
            if (!charge.IsCharging)
                continue;

            var elapsed = Math.Max(
                0f,
                (float) (_timing.CurTime - charge.ChargeStartTime)
                    .TotalSeconds);

            var effect = charge.EffectSpawnedEntity;

            if (effect.IsValid() &&
                TryComp<SpriteComponent>(effect, out var sprite) &&
                _sprite.TryGetLayer(
                    (effect, sprite), 0, out var layer, false) &&
                layer.ActualRsi is { } rsi &&
                rsi.TryGetState(layer.State, out var state))
            {
                _sprite.LayerSetAutoAnimated(layer, false);

                const int readyFrame = 6;
                var targetFrame = Math.Min(readyFrame, state.DelayCount - 1);
                var readyFrameStart = 0f;

                for (var i = 0; i < targetFrame; i++)
                    readyFrameStart += state.GetDelay(i);

                var progress = charge.MaxChargeTime <= 0f
                ? 1f
                : Math.Clamp(elapsed / charge.MaxChargeTime, 0f, 1f);

                var animationTime = progress >= 1f
                ? readyFrameStart + state.GetDelay(targetFrame) / 2f
                : readyFrameStart * progress;

                _sprite.LayerSetAnimationTime(
                    layer, animationTime);
            }

            if (_player.LocalEntity is not { } local ||
                charge.ChargingUser != local)
                continue;

            if (_readyBow != bow ||
                _readyStart != charge.ChargeStartTime)
            {
                _readyBow = bow;
                _readyStart = charge.ChargeStartTime;
                _readySoundPlayed = false;
            }

            if (_readySoundPlayed ||
                elapsed < charge.MaxChargeTime)
                continue;

            _readySoundPlayed = true;

            if (charge.FullChargeSound is { } sound)
                _audio.PlayGlobal(
                    sound, Filter.Local(), true);
        }
    }
}
