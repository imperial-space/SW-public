using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[Prototype("bossEncounterFlow")]
public sealed partial class BossEncounterFlowPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public string MechanicSet = string.Empty;                         // installed mechanic group
    [DataField] public float OpeningDelay = 13;                                  // seconds before the opening step
    [DataField] public float EntryGrace = 15;                                    // late entry window in seconds
    [DataField] public float IdleReset = 15;                                     // seconds before a wipe resets
    [DataField] public float Recovery = 7;                                       // default delay between steps
    [DataField] public float TankInterval = 5;                                   // delay between basic attacks
    [DataField] public DamageSpecifier TankDamage = new();
    [DataField] public Dictionary<string, DamageSpecifier> Damage = new();       // named damage amounts used by steps
    [DataField] public Dictionary<BossTelegraphPattern, string> PatternDamage = new();
    [DataField] public List<BossEncounterStep> Tutorial = new();                  // opening sequence
    [DataField] public List<BossEncounterStep> Rotation = new();                  // repeating sequence
    [DataField] public List<BossEncounterStep> Escalation = new();                // later repeating sequence
}
