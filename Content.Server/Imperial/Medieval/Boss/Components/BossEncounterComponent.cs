using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossEncounterComponent : Component
{
    [DataField]
    public List<BossEncounterPhase> Phases = new();
    public int PhaseIndex;
    [DataField]
    public bool AutoStart = true;
    [DataField]
    public float OpeningDelay = 13;
    [DataField]
    public float EntryGrace = 15;
    [DataField]
    public float IdleReset = 15;
    [DataField]
    public float Recovery = 7;
    [DataField]
    public float TankInterval = 5;
    [DataField]
    public DamageSpecifier TankDamage = new();
    [DataField]
    public Dictionary<string, DamageSpecifier> Damage = new();
    [DataField]
    public Dictionary<BossTelegraphPattern, string> PatternDamage = new();
    [DataField]
    public List<BossEncounterStep> Tutorial = new();
    [DataField]
    public List<BossEncounterStep> Rotation = new();
    [DataField]
    public List<BossEncounterStep> Escalation = new();
    public bool FightRunning;
    public bool Repeating;
    public bool Escalating;
    public List<BossEncounterStep> PendingSteps = new();
    public Dictionary<BossTelegraphPattern, BossStoredTargets> StoredTargets = new();
    public int PendingIndex;
    public bool? LastMixedStackFirst;
    public bool? PendingMixedStackFirst;
    public bool Waiting;
    public bool Resetting;
    public int Step;
    public int Lap;
    public uint Generation;
    public HashSet<EntityUid> Party = new();
    [AutoPausedField]
    public TimeSpan EntryCloses;
    [AutoPausedField]
    public TimeSpan NextStep;
    [AutoPausedField]
    public TimeSpan NextTank;
    [AutoPausedField]
    public TimeSpan? IdleDeadline;
    [AutoPausedField]
    public TimeSpan NextResetAttempt;
    [AutoPausedField]
    public TimeSpan? RainPressureStart;
}
