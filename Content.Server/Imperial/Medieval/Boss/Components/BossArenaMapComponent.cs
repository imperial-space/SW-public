using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossArenaMapComponent : Component
{
    [DataField]
    public string ArenaId = string.Empty;
    [DataField]
    public ProtoId<BossArenaPrototype>? ArenaPrototype;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? BossPrototype;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? SummoningPrototype;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? CenterBlockerPrototype = "BossArenaCenterBlocker";
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? BrazierPrototype;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? VictoryExitPrototype = "BossArenaReturnPortal";
    [DataField]
    public Vector2i VictoryExitTile = new(0, 4);
    [DataField]
    public int HalfSize = 5;
    [DataField]
    public int MaxPlayers = 8;
    [DataField]
    public bool ReplenishQuivers;
    [DataField]
    public string MessagePrefix = "Boss_Encounter_";
    [DataField]
    public Color AmbientLight = Color.FromHex("#6670CF");
    [DataField(required: true)]
    public EntityUid CombatGrid;
    [DataField(required: true)]
    public EntityUid Entrance;
    [DataField]
    public EntityUid Boss;
    [DataField]
    public EntityUid? Crystal;
    [DataField]
    public EntityUid? EntryGate;
    [DataField]
    public EntityUid? VictoryExit;
    [DataField]
    public TimeSpan VictoryLootDuration = TimeSpan.FromMinutes(5);
    [ViewVariables, AutoPausedField]
    public TimeSpan? VictoryDeadline;
    [AutoPausedField]
    public TimeSpan NextVictoryReturn;
    public bool VictoryWarningSent;
    [DataField]
    public List<EntityUid> Braziers = new();
}
