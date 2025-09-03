using System;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Audio;

public sealed partial class ContentAudioSystem
{


    private static float _locationAmbientVolumeSlider = 1.0f;
    private EntityUid? _currentLocationStream;
    private EntityUid? _previousLocationStream;

    private void InitializeLocationAudio()
    {
        Subs.CVar(_configManager, CCVars.LocationAmbientVolume, OnLocationAmbientVolumeChanged, true);

        _locationAmbientVolumeSlider = _configManager.GetCVar(CCVars.LocationAmbientVolume);

        Logger.Info($"LocationAudio initialized with volume: {_locationAmbientVolumeSlider}");
    }

    private void OnLocationAmbientVolumeChanged(float newVolume)
    {
        _locationAmbientVolumeSlider = newVolume;
        Logger.Info($"LocationAmbientVolume changed to: {newVolume}");

        // Обновляем громкость текущего потока
        if (_currentLocationStream != null && _audio.IsPlaying(_currentLocationStream))
        {
            var volumeDb = newVolume <= 0f ? float.NegativeInfinity : SharedAudioSystem.GainToVolume(newVolume);
            _audio.SetVolume(_currentLocationStream, volumeDb);
            Logger.Info($"Updated current location stream volume to: {volumeDb} dB");
        }

        // Обновляем громкость предыдущего потока
        if (_previousLocationStream != null && _audio.IsPlaying(_previousLocationStream))
        {
            var volumeDb = newVolume <= 0f ? float.NegativeInfinity : SharedAudioSystem.GainToVolume(newVolume);
            _audio.SetVolume(_previousLocationStream, volumeDb);
            Logger.Info($"Updated previous location stream volume to: {volumeDb} dB");
        }
    }

    /// <summary>
    /// Воспроизводит основной звук локации
    /// </summary>
    public void PlayLocationSound(string soundPath, bool loop = true)
    {
        // Если громкость 0 или меньше, не воспроизводим звук
        if (_locationAmbientVolumeSlider <= 0f)
        {
            Logger.Info($"LocationAmbientVolume is {_locationAmbientVolumeSlider}, skipping sound playback");
            return;
        }

        // Останавливаем предыдущий поток
        if (_previousLocationStream != null)
        {
            _audio.Stop(_previousLocationStream);
            _previousLocationStream = null;
        }

        // Переносим текущий поток в предыдущий
        _previousLocationStream = _currentLocationStream;
        _currentLocationStream = null;

        // Воспроизводим новый звук
        var volumeDb = SharedAudioSystem.GainToVolume(_locationAmbientVolumeSlider);
        var audioParams = AudioParams.Default.WithLoop(loop).WithVolume(volumeDb);

        var stream = _audio.PlayGlobal(soundPath, Filter.Local(), false, audioParams);
        if (stream != null)
        {
            _currentLocationStream = stream.Value.Entity;
            Logger.Info($"Started location sound with volume: {volumeDb} dB");
        }
    }

    /// <summary>
    /// Воспроизводит случайный звук локации
    /// </summary>
    public void PlayRandomLocationSound(string soundPath)
    {
        // Если громкость 0 или меньше, не воспроизводим звук
        if (_locationAmbientVolumeSlider <= 0f)
        {
            Logger.Info($"LocationAmbientVolume is {_locationAmbientVolumeSlider}, skipping random sound playback");
            return;
        }

        var volumeDb = SharedAudioSystem.GainToVolume(_locationAmbientVolumeSlider);
        var audioParams = AudioParams.Default.WithVolume(volumeDb);

        _audio.PlayGlobal(soundPath, Filter.Local(), false, audioParams);
        Logger.Info($"Played random location sound with volume: {volumeDb} dB");
    }

    /// <summary>
    /// Останавливает все локационные звуки
    /// </summary>
    public void StopAllLocationAudio()
    {
        if (_currentLocationStream != null)
        {
            _audio.Stop(_currentLocationStream);
            _currentLocationStream = null;
        }

        if (_previousLocationStream != null)
        {
            _audio.Stop(_previousLocationStream);
            _previousLocationStream = null;
        }

        Logger.Info("Stopped all location audio");
    }
}
