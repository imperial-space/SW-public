using System;
using Content.Client.Audio;
using Content.Shared.Imperial.Audio.Events;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Audio;

/// <summary>
/// Клиентская система для управления локационными звуками
/// </summary>
public sealed class LocationAudioClientSystem : EntitySystem
{
    [Dependency] private readonly ContentAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Подписываемся на сетевые события от сервера
        SubscribeNetworkEvent<PlayLocationSoundEvent>(OnPlayLocationSound);
        SubscribeNetworkEvent<PlayRandomLocationSoundEvent>(OnPlayRandomLocationSound);
        SubscribeNetworkEvent<StopAllLocationAudioEvent>(OnStopAllLocationAudio);

        Logger.Info("LocationAudioClientSystem initialized");
    }

    private void OnPlayLocationSound(PlayLocationSoundEvent ev)
    {
        Logger.Info($"Received PlayLocationSoundEvent: {ev.SoundPath}, loop: {ev.Loop}");
        _audio.PlayLocationSound(ev.SoundPath, ev.Loop);
    }

    private void OnPlayRandomLocationSound(PlayRandomLocationSoundEvent ev)
    {
        Logger.Info($"Received PlayRandomLocationSoundEvent: {ev.SoundPath}");
        _audio.PlayRandomLocationSound(ev.SoundPath);
    }

    private void OnStopAllLocationAudio(StopAllLocationAudioEvent ev)
    {
        Logger.Info("Received StopAllLocationAudioEvent");
        _audio.StopAllLocationAudio();
    }
}
