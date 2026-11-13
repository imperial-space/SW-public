using System;
using Robust.Client.Animations;

namespace Content.Client.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Uses public track APIs to sample an existing animation at absolute elapsed
/// time. No access to the engine's internal playback dictionary is required.
/// </summary>
public sealed class AdvancedMeleeAnimationPlayback
{
    private readonly Animation _animation;
    private readonly object _context;
    private readonly (int Index, float Playing)[] _tracks;
    private float _elapsed;
    private bool _sampled;

    public float Duration => (float) _animation.Length.TotalSeconds;

    public AdvancedMeleeAnimationPlayback(Animation animation, object context)
    {
        _animation = animation;
        _context = context;
        _tracks = new (int, float)[animation.AnimationTracks.Count];
        for (var i = 0; i < _tracks.Length; i++)
            _tracks[i] = animation.AnimationTracks[i].InitPlayback();
    }

    public void Sample(float elapsed)
    {
        if (!float.IsFinite(elapsed))
            return;
        var time = Math.Clamp(elapsed, 0f, MathF.Max(0f, Duration));
        if (_sampled && time <= _elapsed)
            return;
        var delta = time - _elapsed;
        if (!_sampled && delta == 0f)
            delta = float.Epsilon; // Apply zero-time property keyframes as well.
        for (var i = 0; i < _tracks.Length; i++)
        {
            var track = _tracks[i];
            _tracks[i] = _animation.AnimationTracks[i].AdvancePlayback(_context, track.Index, track.Playing, delta);
        }
        _elapsed = time;
        _sampled = true;
    }
}
