using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossMusicPlaybackSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AudioSystem));
        SubscribeLocalEvent<BossMusicStreamComponent, ComponentStartup>(OnMusicStartup);
        SubscribeLocalEvent<BossMusicStreamComponent, AfterAutoHandleStateEvent>(OnMusicState);
    }

    private void OnMusicStartup(EntityUid uid, BossMusicStreamComponent music, ComponentStartup args)
        => Refresh(uid, music);

    private void OnMusicState(EntityUid uid, BossMusicStreamComponent music, ref AfterAutoHandleStateEvent args)
        => Refresh(uid, music);

    private void Refresh(EntityUid uid, BossMusicStreamComponent music)
    {
        if (!string.IsNullOrEmpty(music.PreloadTrack))
            _resources.TryGetResource<AudioResource>(new ResPath(music.PreloadTrack), out _);
        if (music.ClientConfigured && music.ClientStartTime == music.StartTime && music.ClientLoopLength == music.LoopLength &&
            music.ClientTrackLength == music.TrackLength)
            return;
        music.ClientConfigured = true;
        music.ClientStartTime = music.StartTime;
        music.ClientLoopLength = music.LoopLength;
        music.ClientTrackLength = music.TrackLength;
        music.ClientStarted = false;
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<BossMusicStreamComponent, AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var music, out var audio, out var transform))
        {
            var duration = music.TrackLength > TimeSpan.Zero ? music.TrackLength : music.LoopLength;
            if (duration <= TimeSpan.Zero || !audio.Loaded || MetaData(uid).EntityPaused)
                continue;
            if (_timing.CurTime < music.StartTime)
            {
                audio.Gain = 0;
                continue;
            }
            if (music.LoopLength <= TimeSpan.Zero && _timing.CurTime >= music.StartTime + duration)
            {
                audio.Gain = 0;
                continue;
            }
            if (music.ClientStarted && audio.Playing) continue;
            audio.Gain = 0;
            if (!audio.Playing) _audio.SetState(uid, AudioState.Playing, force: true);
            _audio.SetPlaybackPosition(uid, (float)music.Position(_timing.CurTime));
            music.ClientStarted = true;
            if (_audio.GetListenerCoordinates().MapId == transform.MapID)
                audio.Volume = audio.Params.Volume;
        }
    }
}
