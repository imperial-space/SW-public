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
    public EntityUid? Destination;
    [DataField]
    public ResPath? ArenaMapPath;
    [DataField]
    public ProtoId<BossArenaPrototype>? ArenaPrototype;
    [DataField]
    public string ArenaId = string.Empty;
    [DataField]
    public string MessagePrefix = "Boss_Encounter_";
    [DataField]
    public bool ReturnToGate;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? SpectatorOrbPrototype;
    [DataField]
    public Vector2 SpectatorOrbOffset = new(2, 0);
    public EntityUid? SpectatorOrb;
    [DataField]
    public TimeSpan UnlockDuration = TimeSpan.FromSeconds(3);
    [DataField]
    public TimeSpan EntryDuration = TimeSpan.FromSeconds(5);
    [ViewVariables]
    public uint Revision;
}
