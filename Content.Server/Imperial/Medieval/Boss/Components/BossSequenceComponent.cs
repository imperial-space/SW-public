using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossSequenceComponent : Component
{
    public BossPreparedSequenceAttack Attack = default!;
    public List<BossTelegraphPattern> Queue = new();
    public EntityUid Arena;
    public EntityUid Grid;
    public int Facing;
    public int Index;
    public int StrikesResolved;
    public bool Preview;
    public bool Completed;
    public float Interval;
    public BossSequencePhase Phase;
    [AutoPausedField]
    public TimeSpan Deadline;
    [AutoPausedField]
    public TimeSpan PhaseStart;
    [AutoPausedField]
    public TimeSpan LastImpact;
}
