using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocationTriggerComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<LocationTriggerComponent, EndCollideEvent>(OnTriggerExit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Логику затухания/нарастания звуков
        var queryPlayers = EntityQueryEnumerator<PlayerLocationComponent>();
        while (queryPlayers.MoveNext(out var playerUid, out var playerComp))
        {
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
                // Плавное затухание
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

    /// <summary>
    /// Обрабатывает вход игрока в зону триггера
    /// </summary>
    private void OnTriggerEnter(EntityUid uid, LocationTriggerComponent component, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != component.FixtureId)
            return;

        if (!HasComp<PlayerLocationComponent>(args.OtherEntity))
            return;

        var playerComp = Comp<PlayerLocationComponent>(args.OtherEntity);

        if (playerComp.CurrentLocationId == component.LocationId)
            return;

        playerComp.PreviousLocationId = playerComp.CurrentLocationId;
        playerComp.CurrentLocationId = component.LocationId;

        if (playerComp.PreviousStream != null)
        {
            _audio.Stop(playerComp.PreviousStream);
            playerComp.PreviousStream = null;
        }

        playerComp.PreviousStream = playerComp.CurrentStream;
        playerComp.CurrentStream = null;

        if (!string.IsNullOrEmpty(component.LocationId) && component.Sound != null)
        {
            var startParams = AudioParams.Default.WithLoop(true).WithVolume(-40f);
            var stream = _audio.PlayGlobal(component.Sound, args.OtherEntity, startParams);
            if (stream != null)
            {
                playerComp.CurrentStream = stream.Value.Entity;
            }
        }
    }

    /// <summary>
    /// Обрабатывает выход игрока из зоны триггера
    /// </summary>
    private void OnTriggerExit(EntityUid uid, LocationTriggerComponent component, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != component.FixtureId)
            return;

        if (!HasComp<PlayerLocationComponent>(args.OtherEntity))
            return;

        var playerComp = Comp<PlayerLocationComponent>(args.OtherEntity);

        if (playerComp.CurrentLocationId == component.LocationId)
        {
            playerComp.CurrentLocationId = string.Empty;

            if (playerComp.CurrentStream != null)
            {
                _audio.Stop(playerComp.CurrentStream);
                playerComp.CurrentStream = null;
            }
        }
    }

    // TODO: Система день/ночь для эмбиента
    /// <summary>
    /// Выбирает подходящий звук в зависимости от времени суток
    /// TODO: Интегрировать с приватной системой времени суток
    /// </summary>
    // private SoundSpecifier? GetTimeBasedSound(LocationTriggerComponent trigger)
    // {
    //     if (!trigger.EnableTimeBasedAudio)
    //         return trigger.Sound;
    //
    //     var currentHour = 12;
    //
    //     if (currentHour >= trigger.DayStartHour && currentHour < trigger.NightStartHour)
    //     {
    //         return trigger.DaySound ?? trigger.Sound;
    //     }
    //     else
    //     {
    //         return trigger.NightSound ?? trigger.Sound;
    //     }
    // }

    private float GetVolume(EntityUid? stream)
    {
        if (stream == null)
            return float.NegativeInfinity;

        if (!TryComp<AudioComponent>(stream.Value, out var comp))
            return float.NegativeInfinity;

        return comp.Volume;
    }
}
