using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossMusicSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BossTelegraphSystem _arenas = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
        SubscribeLocalEvent<BossFightEvent>(OnFight);
        SubscribeLocalEvent<BossArenaStateEvent>(OnArena);
        SubscribeLocalEvent<BossArenaMusicComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnFight(ref BossFightEvent args)
    {
        if (args.Running) Begin(args.Boss);
        else StopFight(args.Boss);
    }

    private void OnArena(ref BossArenaStateEvent args)
    {
        if (args.State == BossArenaState.Reset) StopArena(args.Arena);
    }

    public void Begin(EntityUid boss)
    {
        if (!TryComp<BossMusicComponent>(boss, out var profile) || !_arenas.TryGetArena(boss, out var arena)) return;
        var music = EnsureComp<BossArenaMusicComponent>(arena.Owner);
        if (!TryResolve(profile, music.SelectedSoundtrack, out var intro, out var loop, out _, out var soundtrack)) return;
        Stop(arena.Owner, music);
        music.Boss = boss;
        music.ActiveSoundtrack = soundtrack;
        music.Loop = loop;
        Play(arena.Owner, music, intro, BossMusicPhase.Intro);
    }

    public bool TrySetSoundtrack(EntityUid arena, ProtoId<BossSoundtrackPrototype> soundtrack)
    {
        if (!HasComp<BossArenaMapComponent>(arena) || Terminating(arena) ||
            !_prototypes.TryIndex(soundtrack, out var profile)) return false;
        var music = EnsureComp<BossArenaMusicComponent>(arena);
        music.SelectedSoundtrack = soundtrack;
        if (music.ActiveSoundtrack == soundtrack) return true;
        var phase = music.Phase;
        Stop(arena, music);
        music.ActiveSoundtrack = soundtrack;
        music.Loop = profile.Loop;
        if (phase != BossMusicPhase.Stopped)
            Play(arena, music, phase switch
            {
                BossMusicPhase.Intro => profile.Intro,
                BossMusicPhase.Loop => profile.Loop,
                _ => profile.Outro
            }, phase);
        return true;
    }

    private bool TryResolve(BossMusicComponent profile, ProtoId<BossSoundtrackPrototype>? selected,
        out SoundSpecifier intro, out SoundSpecifier loop, out SoundSpecifier outro,
        out ProtoId<BossSoundtrackPrototype>? soundtrack)
    {
        soundtrack = selected ?? profile.Soundtrack;
        if (soundtrack is { } id && _prototypes.TryIndex(id, out var version))
        {
            intro = version.Intro;
            loop = version.Loop;
            outro = version.Outro;
            return true;
        }
        intro = profile.Intro!;
        loop = profile.Loop!;
        outro = profile.Outro!;
        return soundtrack == null && intro != null && loop != null && outro != null;
    }

    public void StopFight(EntityUid boss)
    {
        if (_arenas.TryGetArena(boss, out var arena) && TryComp<BossArenaMusicComponent>(arena.Owner, out var music) &&
            music.Boss == boss && music.Phase != BossMusicPhase.Outro)
            Stop(arena.Owner, music);
    }

    public void StopArena(EntityUid arena)
    {
        if (TryComp<BossArenaMusicComponent>(arena, out var music)) Stop(arena, music);
    }

    private void OnDefeated(ref BossDefeatedEvent args)
    {
        if (!TryComp<BossMusicComponent>(args.Boss, out var profile) || !_arenas.TryGetArena(args.Boss, out var arena)) return;
        var music = EnsureComp<BossArenaMusicComponent>(arena.Owner);
        if (!TryResolve(profile, music.SelectedSoundtrack, out _, out _, out var outro, out var soundtrack)) return;
        Stop(arena.Owner, music);
        music.Boss = args.Boss;
        music.ActiveSoundtrack = soundtrack;
        Play(arena.Owner, music, outro, BossMusicPhase.Outro);
    }

    private void Play(EntityUid arena, BossArenaMusicComponent music, SoundSpecifier track, BossMusicPhase phase)
    {

        var resolved = _audio.ResolveSound(track);
        var loop = phase == BossMusicPhase.Loop;
        var stream = CreateStream(arena, resolved, track.Params.WithLoop(loop), _timing.CurTime,
            phase == BossMusicPhase.Intro ? _audio.GetAudioPath(_audio.ResolveSound(music.Loop)) : null);
        if (stream == null) return;
        music.Stream = stream;
        music.Phase = phase;
        var length = _audio.GetAudioLength(resolved);
        music.TransitionAt = loop ? null : _timing.CurTime + length;
        if (loop)
        {
            music.LoopLength = length;
            music.LoopStartedAt = _timing.CurTime;
        }
    }

    private EntityUid? CreateStream(EntityUid arena, ResolvedSoundSpecifier resolved, AudioParams parameters,
        TimeSpan start, string? preload = null)
    {
        var stream = _audio.PlayPvs(resolved, arena, parameters);
        if (stream == null) return null;
        _audio.SetMapAudio(stream);                                              // play only to listeners on this arena map
        var playback = EnsureComp<BossMusicStreamComponent>(stream.Value.Entity);
        playback.StartTime = start;
        playback.TrackLength = _audio.GetAudioLength(resolved);
        playback.LoopLength = parameters.Loop ? playback.TrackLength : TimeSpan.Zero;
        playback.PreloadTrack = preload ?? string.Empty;
        Dirty(stream.Value.Entity, playback);
        return stream.Value.Entity;
    }

    private void Stop(EntityUid arena, BossArenaMusicComponent music)
    {

        _audio.Stop(music.Stream);
        _audio.Stop(music.PendingLoop);
        music.Stream = null;
        music.PendingLoop = null;
        music.Phase = BossMusicPhase.Stopped;
        music.TransitionAt = null;
    }

    private void OnShutdown(EntityUid arena, BossArenaMusicComponent music, ComponentShutdown args)
        => Stop(arena, music);

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BossArenaMusicComponent>();
        while (query.MoveNext(out var arena, out var music))
        {
            if (MetaData(arena).EntityPaused || music.Phase == BossMusicPhase.Loop || music.TransitionAt is not { } deadline) continue;
            if (music.Phase == BossMusicPhase.Intro && _timing.CurTime >= deadline - TimeSpan.FromSeconds(0.75) &&
                TryComp<BossComponent>(music.Boss, out var activeBoss) && activeBoss.Active && !activeBoss.Finished)
            {
                if (music.PendingLoop == null)
                    music.PendingLoop = CreateStream(arena, _audio.ResolveSound(music.Loop), music.Loop.Params.WithLoop(true), deadline);
                if (_timing.CurTime < deadline) continue;
                if (music.PendingLoop is { } prepared && Exists(prepared) && !Terminating(prepared))
                {
                    _audio.Stop(music.Stream);
                    music.Stream = prepared;
                    music.PendingLoop = null;
                    music.Phase = BossMusicPhase.Loop;
                    music.LoopLength = Comp<BossMusicStreamComponent>(prepared).LoopLength;
                    music.LoopStartedAt = deadline;
                    music.TransitionAt = null;
                    continue;
                }
            }
            if (_timing.CurTime < deadline) continue;
            var phase = music.Phase;
            var loop = music.Loop;
            Stop(arena, music);
            if (phase == BossMusicPhase.Intro && TryComp<BossComponent>(music.Boss, out var boss) && boss.Active && !boss.Finished)
                Play(arena, music, loop, BossMusicPhase.Loop);
        }
    }
}
