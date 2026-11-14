using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossSequenceComponent : Component
{
    public BossPreparedSequenceAttack Attack = default!;
    public List<BossTelegraphPattern> Queue = new();                             // stored patterns in strike order
    public EntityUid Arena;
    public EntityUid Grid;
    public int Facing;
    public int Index;
    public int StrikesResolved;
    public bool Preview;
    public bool Completed;
    public float Interval;                                                       // seconds between stored strikes
    public BossSequencePhase Phase;
    [AutoPausedField]
    public TimeSpan Deadline;                                                    // time of the next phase change
    [AutoPausedField]
    public TimeSpan PhaseStart;
    [AutoPausedField]
    public TimeSpan LastImpact;
}
