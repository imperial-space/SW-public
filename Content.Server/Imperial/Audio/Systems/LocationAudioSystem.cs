using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Audio;

/// <summary>
/// Система управления звуками по зонам и плавным переходом.
/// </summary>
public sealed class LocationAudioSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var queryPlayers = EntityQueryEnumerator<PlayerLocationComponent>();
        while (queryPlayers.MoveNext(out var playerUid, out var playerComp))
        {
            // Поиск ближайшего триггера в радиусе активации
            EntityUid? bestTrigger = null;
            float bestDistance = float.MaxValue;

            var queryTriggers = EntityQueryEnumerator<LocationTriggerComponent>();
            while (queryTriggers.MoveNext(out var triggerUid, out var triggerComp))
            {
                var distance = GetWorldDistance(playerUid, triggerUid);
                if (distance <= triggerComp.ActivationDistance && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTrigger = triggerUid;
                }
            }

            // Определяем желаемую локацию и звук
            string desiredLocationId = string.Empty;
            SoundSpecifier? desiredSound = null;
            if (bestTrigger != null && TryComp(bestTrigger.Value, out LocationTriggerComponent? chosen))
            {
                desiredLocationId = chosen.LocationId;
                desiredSound = chosen.Sound;
            }

            // Если игрок сменил зону — переключаем звуки
            if (playerComp.CurrentLocationId != desiredLocationId)
            {
                playerComp.PreviousLocationId = playerComp.CurrentLocationId;
                playerComp.CurrentLocationId = desiredLocationId;

                if (playerComp.PreviousStream != null)
                {
                    _audio.Stop(playerComp.PreviousStream);
                    playerComp.PreviousStream = null;
                }

                playerComp.PreviousStream = playerComp.CurrentStream;
                playerComp.CurrentStream = null;

                // Запускаем новый поток
                if (!string.IsNullOrEmpty(desiredLocationId) && desiredSound != null)
                {
                    var startParams = AudioParams.Default.WithLoop(true).WithVolume(-40f);
                    var stream = _audio.PlayGlobal(desiredSound, playerUid, startParams);
                    if (stream != null)
                    {
                        playerComp.CurrentStream = stream.Value.Entity;
                    }
                }
            }

            // Логика затухания/нарастания
            var fadeRate = playerComp.FadeRateDbPerSec;
            if (playerComp.CurrentStream != null && _audio.IsPlaying(playerComp.CurrentStream))
            {
                // Плавное нарастание громкости
                _audio.SetVolume(playerComp.CurrentStream,
                    MathF.Min(playerComp.TargetVolumeDb,
                        GetVolume(playerComp.CurrentStream) + fadeRate * frameTime));
            }

            if (playerComp.PreviousStream != null)
            {
                // Плавное затухание, остановка на -80 дБ
                var newVol = GetVolume(playerComp.PreviousStream) - fadeRate * frameTime;
                _audio.SetVolume(playerComp.PreviousStream, newVol);
                if (newVol <= -80f)
                {
                    _audio.Stop(playerComp.PreviousStream);
                    playerComp.PreviousStream = null;
                }
            }
        }
    }

    private float GetWorldDistance(EntityUid a, EntityUid b)
    {
        var xa = _xform.GetWorldPosition(a);
        var xb = _xform.GetWorldPosition(b);
        return (xa - xb).Length();
    }

    private float GetVolume(EntityUid? stream)
    {
        if (stream == null)
            return float.NegativeInfinity;

        if (!TryComp<AudioComponent>(stream.Value, out var comp))
            return float.NegativeInfinity;

        return comp.Volume;
    }
}
