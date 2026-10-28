using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Boss;

[Prototype("bossArena")]
public sealed partial class BossArenaPrototype : IPrototype
{
    public const int MinHalfSize = 2;
    public const int MaxHalfSize = 32;
    public const int MaxPartySize = 8;

    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField(required: true)]
    public EntProtoId BossPrototype;
    [DataField]
    public int HalfSize = 5;
    [DataField]
    public ProtoId<ContentTileDefinition> Floor = "BossArenaFloor";
    [DataField]
    public EntProtoId EntrancePrototype = "BossArenaEntrance";
    [DataField]
    public Vector2i? EntranceTile;
    [DataField]
    public EntProtoId? SummoningPrototype = "BossArenaSummoningTrigger";
    [DataField]
    public EntProtoId? CenterBlockerPrototype = "BossArenaCenterBlocker";
    [DataField]
    public EntProtoId? BrazierPrototype;
    [DataField]
    public EntProtoId? VictoryExitPrototype = "BossArenaReturnPortal";
    [DataField]
    public Vector2i? VictoryExitTile;
    [DataField]
    public int MaxPlayers = MaxPartySize;
    [DataField]
    public bool ReplenishQuivers;
    [DataField]
    public string MessagePrefix = "Boss_Encounter_";
    [DataField]
    public Color AmbientLight = Color.FromHex("#CCCCCC");
    [DataField]
    public TimeSpan VictoryLootDuration = TimeSpan.FromMinutes(5);

    public Vector2i ArrivalTile => EntranceTile ?? new Vector2i(0, 1 - HalfSize);
    public Vector2i ExitTile => VictoryExitTile ?? new Vector2i(0, HalfSize - 1);
}
