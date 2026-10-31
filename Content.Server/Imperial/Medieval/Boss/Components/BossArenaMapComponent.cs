using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossArenaMapComponent : Component
{
    [DataField]
    public string ArenaId = string.Empty;                                        // matches the arena content definitions
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? BossPrototype;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? SummoningPrototype;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? CenterBlockerPrototype = "BossArenaCenterBlocker";
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? BrazierPrototype;
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? VictoryExitPrototype;
    [DataField]
    public Vector2i VictoryExitTile = new(0, 4);                                // Return portal tile
    [DataField]
    public int HalfSize = 5;                                                    // Arena half-size in tiles
    [DataField]
    public int MaxPlayers = 8;                                                  // Player limit
    [DataField]
    public bool ReplenishQuivers;
    [DataField]
    public string MessagePrefix = "Boss_Encounter_";
    [DataField]
    public Color AmbientLight = Color.FromHex("#6670CF");                       // Arena light color
    [DataField(required: true)]
    public EntityUid CombatGrid;                                                // Arena floor
    [DataField(required: true)]
    public EntityUid Entrance;                                                  // Arrival point
    [DataField]
    public EntityUid Boss;                                                       // boss belonging to this arena
    [DataField]
    public EntityUid? Crystal;
    [DataField]
    public EntityUid? EntryGate;                                                 // gate that owns this arena instance
    [DataField]
    public EntityUid? VictoryExit;
    [DataField]
    public TimeSpan VictoryLootDuration = TimeSpan.FromMinutes(5);              // Time to collect loot
    [ViewVariables, AutoPausedField]
    public TimeSpan? VictoryDeadline;
    [AutoPausedField]
    public TimeSpan NextVictoryReturn;
    public bool VictoryWarningSent;
    [DataField]
    public List<EntityUid> Braziers = new();
}
