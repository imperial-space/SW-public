using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossMusicStreamComponent : Component
{
    [AutoNetworkedField, AutoPausedField] public TimeSpan StartTime;
    [AutoNetworkedField] public TimeSpan LoopLength;
    [AutoNetworkedField] public TimeSpan TrackLength;
    [AutoNetworkedField] public string PreloadTrack = string.Empty;
    public bool ClientStarted;
    public bool ClientConfigured;
    public TimeSpan ClientStartTime;
    public TimeSpan ClientLoopLength;
    public TimeSpan ClientTrackLength;

    public double Position(TimeSpan now)
    {
        var elapsed = Math.Max(0, (now - StartTime).TotalSeconds);
        return LoopLength > TimeSpan.Zero ? elapsed % LoopLength.TotalSeconds : elapsed;
    }
}
