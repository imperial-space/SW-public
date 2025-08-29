using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Audio;

/// <summary>
/// Система управления звуками по зонам и плавным переходом.
/// </summary>
public sealed class LocationAudioSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocationTriggerComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<LocationTriggerComponent, EndCollideEvent>(OnTriggerExit);
    }

    /// <summary>
    /// Запускает таймер для случайного звука
    /// </summary>
    private void StartRandomSoundTimer(EntityUid player, PlayerLocationComponent comp, LocationTriggerComponent trigger)
    {
        if (trigger.RandomSounds.Count == 0)
            return;

        comp.RandomSoundVersion++;
        var version = comp.RandomSoundVersion;

        void PlayRandomSound()
        {
            if (!EntityManager.EntityExists(player))
                return;

            if (!TryComp(player, out PlayerLocationComponent? c) || c != comp)
                return;

            if (version != comp.RandomSoundVersion)
                return;

            // Проверяем, что игрок все еще в той же локации
            if (comp.CurrentLocationId != trigger.LocationId)
                return;

            // Выбираем случайный звук
            var randomSound = _random.Pick(trigger.RandomSounds);

            // Воспроизводим звук
            var audioParams = AudioParams.Default.WithVolume(-12f);
            _audio.PlayGlobal(randomSound, player, audioParams);

            // Устанавливаем следующий таймер
            var minInterval = TimeSpan.FromSeconds(trigger.MinRandomIntervalSeconds);
            var maxInterval = TimeSpan.FromSeconds(trigger.MaxRandomIntervalSeconds);
            var randomInterval = _random.Next(minInterval, maxInterval);

            Timer.Spawn(randomInterval, PlayRandomSound);
        }

        // Устанавливаем первый таймер
        var firstMinInterval = TimeSpan.FromSeconds(trigger.MinRandomIntervalSeconds);
        var firstMaxInterval = TimeSpan.FromSeconds(trigger.MaxRandomIntervalSeconds);
        var firstRandomInterval = _random.Next(firstMinInterval, firstMaxInterval);

        Timer.Spawn(firstRandomInterval, PlayRandomSound);
    }

    /// <summary>
    /// Запускает плавное нарастание громкости для текущего потока.
    /// Использует версионность, чтобы отменять старые таймеры.
    /// </summary>
    private void StartFadeIn(EntityUid player, PlayerLocationComponent comp)
    {
        if (comp.CurrentStream == null)
            return;

        comp.CurrentFadeVersion++;
        var version = comp.CurrentFadeVersion;

        void Step()
        {
            if (!EntityManager.EntityExists(player))
                return;

            if (!TryComp(player, out PlayerLocationComponent? c) || c != comp)
                return;

            if (version != comp.CurrentFadeVersion)
                return;

            var stream = comp.CurrentStream;
            if (stream == null || !_audio.IsPlaying(stream))
                return;

            var fadeRate = comp.FadeRateDbPerSec;
            var vol = GetVolume(stream);
            var newVol = MathF.Min(comp.TargetVolumeDb, vol + fadeRate * 0.1f);
            _audio.SetVolume(stream, newVol);

            if (newVol < comp.TargetVolumeDb)
            {
                Timer.Spawn(TimeSpan.FromSeconds(0.1), Step);
            }
        }

        Timer.Spawn(TimeSpan.FromSeconds(0.1), Step);
    }

    /// <summary>
    /// Запускает плавное затухание громкости для предыдущего потока.
    /// Использует версионность, чтобы отменять старые таймеры.
    /// </summary>
    private void StartFadeOutPrevious(EntityUid player, PlayerLocationComponent comp)
    {
        if (comp.PreviousStream == null)
            return;

        comp.PreviousFadeVersion++;
        var version = comp.PreviousFadeVersion;

        void Step()
        {
            if (!EntityManager.EntityExists(player))
                return;

            if (!TryComp(player, out PlayerLocationComponent? c) || c != comp)
                return;

            if (version != comp.PreviousFadeVersion)
                return;

            var stream = comp.PreviousStream;
            if (stream == null)
                return;

            var fadeRate = comp.FadeRateDbPerSec;
            var vol = GetVolume(stream);
            var newVol = vol - fadeRate * 0.1f;
            _audio.SetVolume(stream, newVol);

            if (newVol <= -80f)
            {
                _audio.Stop(stream);
                comp.PreviousStream = null;
                return;
            }

            Timer.Spawn(TimeSpan.FromSeconds(0.1), Step);
        }

        Timer.Spawn(TimeSpan.FromSeconds(0.1), Step);
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

        // Сбрасываем таймер случайных звуков
        playerComp.RandomSoundVersion++;

        // перенесём текущий в предыдущий и запустим затухание
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
                // запустим плавное нарастание нового звука и затухание предыдущего
                StartFadeIn(args.OtherEntity, playerComp);
                StartFadeOutPrevious(args.OtherEntity, playerComp);
            }
        }

        // Запускаем таймер случайных звуков
        StartRandomSoundTimer(args.OtherEntity, playerComp, component);
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
            // Сбрасываем таймер случайных звуков
            playerComp.RandomSoundVersion++;

            // перенесём текущий поток в предыдущий и запустим затухание
            if (playerComp.PreviousStream != null)
            {
                _audio.Stop(playerComp.PreviousStream);
                playerComp.PreviousStream = null;
            }
            playerComp.PreviousStream = playerComp.CurrentStream;
            playerComp.CurrentStream = null;
            StartFadeOutPrevious(args.OtherEntity, playerComp);
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
