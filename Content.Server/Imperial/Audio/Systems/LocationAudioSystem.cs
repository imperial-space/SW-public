using Content.Shared.Imperial.ICCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Audio;

/// <summary>
/// Система управления звуками по зонам и плавным переходом.
/// </summary>
public sealed class LocationAudioSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocationTriggerComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    /// <summary>
    /// Обрабатывает отключение игрока от сущности
    /// </summary>
    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!TryComp<PlayerLocationComponent>(ev.Entity, out var playerComp))
            return;

        StopAllAudio(ev.Entity, playerComp);
    }

    /// <summary>
    /// Обрабатывает подключение игрока к новой сущности
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!TryComp<PlayerLocationComponent>(ev.Entity, out var playerComp))
            return;

        // Сбрасываем состояние при подключении к новой сущности
        playerComp.CurrentLocationId = string.Empty;
        playerComp.PreviousLocationId = string.Empty;
        playerComp.RandomSoundVersion++;
        playerComp.CurrentFadeVersion++;
        playerComp.PreviousFadeVersion++;

        StopAllAudio(ev.Entity, playerComp);
    }

    /// <summary>
    /// Останавливает все звуки для игрока
    /// </summary>
    private void StopAllAudio(EntityUid player, PlayerLocationComponent comp)
    {
        // Останавливаем текущий поток
        if (comp.CurrentStream != null)
        {
            _audio.Stop(comp.CurrentStream.Value);
            comp.CurrentStream = null;
        }

        // Останавливаем предыдущий поток
        if (comp.PreviousStream != null)
        {
            _audio.Stop(comp.PreviousStream.Value);
            comp.PreviousStream = null;
        }

        // Сбрасываем состояние
        comp.CurrentLocationId = string.Empty;
        comp.PreviousLocationId = string.Empty;
        comp.RandomSoundVersion++;
        comp.CurrentFadeVersion++;
        comp.PreviousFadeVersion++;
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

        if (!TryComp<PlayerLocationComponent>(args.OtherEntity, out var playerComp))
            return;

        if (playerComp.CurrentLocationId == component.LocationId)
            return;

        playerComp.PreviousLocationId = playerComp.CurrentLocationId;
        playerComp.CurrentLocationId = component.LocationId;

        playerComp.RandomSoundVersion++;

        // перенесём текущий в предыдущий и запустим затухание
        if (playerComp.PreviousStream != null)
        {
            StartFadeOutPrevious(args.OtherEntity, playerComp);
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

            if (comp.CurrentLocationId != trigger.LocationId)
                return;

            var randomSound = _random.Pick(trigger.RandomSounds);

            var audioParams = AudioParams.Default.WithVolume(_cfg.GetCVar(ICCVars.LocationAmbientVolume));
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
}
