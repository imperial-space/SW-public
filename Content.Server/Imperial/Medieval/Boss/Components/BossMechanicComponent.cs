using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossMechanicComponent : Component
{
    public BossTilePatternAttack Attack = default!;
    public BossTilePatternAttack? SimultaneousAttack;
    public EntityUid Arena;
    public EntityUid Grid;
    public bool SecondPhase;
    public bool Completed;
    public uint Generation;
    public List<EntityUid> Targets = new();
    public Color? OriginalLight;
    public bool Blackout;
    public bool BlackoutRelocated;
    public bool BlackoutRecovery;
    public bool BlackoutReturnStarted;
    public EntityUid? BlackoutImpact;
    public bool PreloadOnly;
    [AutoPausedField]
    public TimeSpan BlackoutMoveTime;
    [AutoPausedField]
    public TimeSpan Deadline;
    [AutoPausedField]
    public TimeSpan Start;
}
