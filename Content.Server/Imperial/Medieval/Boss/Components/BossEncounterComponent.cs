using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossEncounterComponent : Component
{
    [DataField]
    public ProtoId<BossEncounterFlowPrototype>? Flow;                             // select the boss flow without changing its identity
    public string? ActiveFlow;
    public BossEncounterStep? RunningStep;

    [DataField]
    public string MechanicSet = string.Empty;                                    // mechanic group used by this boss

    [DataField]
    public bool AutoStart = true;
    [DataField]
    public float OpeningDelay = 13;                                              // seconds before the first mechanic
    [DataField]
    public float EntryGrace = 15;                                                // seconds before late entry closes
    [DataField]
    public float IdleReset = 15;                                                 // seconds without living players before reset
    [DataField]
    public float Recovery = 7;                                                   // default seconds between mechanics
    [DataField]
    public float TankInterval = 5;                                               // seconds between basic tank attacks
    [DataField]
    public DamageSpecifier TankDamage = new();
    [DataField]
    public Dictionary<string, DamageSpecifier> Damage = new();
    [DataField]
    public Dictionary<BossTelegraphPattern, string> PatternDamage = new();
    [DataField]
    public List<BossEncounterStep> Tutorial = new();                             // opening sequence, played once
    [DataField]
    public List<BossEncounterStep> Rotation = new();                             // repeating mechanic sequence
    [DataField]
    public List<BossEncounterStep> Escalation = new();                           // harder sequence after the normal rotation
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
    public uint Generation;                                                      // used to reject projectiles from earlier fights
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
