using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaGateComponent : Component
{
    [DataField]
    public bool Open;
    public bool CombatLocked;
    public bool EntryLocked;
    [DataField]
    public EntityUid? Destination;                                               // entrance linked to this gate
    [DataField]
    public ResPath? ArenaMapPath;
    [DataField]
    public string ArenaId = string.Empty;
    [DataField]
    public string MessagePrefix = "Boss_Encounter_";
    [DataField]
    public bool ReturnToGate;                                                    // uses the visitor's original entry gate
    [DataField]
    public TimeSpan UnlockDuration = TimeSpan.FromSeconds(3);                    // time needed to unlock the gate
    [DataField]
    public TimeSpan EntryDuration = TimeSpan.FromSeconds(5);                     // time needed to pass through the gate
    [ViewVariables]
    public uint Revision;
}
