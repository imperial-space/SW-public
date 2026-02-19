using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.MagicDungeon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DungeonPortalFrameComponent : Component
{
    [ViewVariables]
    public bool IsActive;

    [ViewVariables]
    public bool IsOpened;

    [ViewVariables]
    public HashSet<EntityUid> PlayersInside;

    [ViewVariables, AutoNetworkedField]
    public MapId DungeonMap;

    [ViewVariables, AutoNetworkedField]
    public List<EntityCoordinates> CoordForSpawnList = new();

    [ViewVariables]
    public EntityUid? PortalId;

    [DataField]
    public Vector2i BaseSize = (6, 6);

    [DataField("shardSlot1")]
    public string ShardSlotId1 = "shard1";

    [ViewVariables(VVAccess.ReadWrite)]
    public ItemSlot ShardSlot1 = default!;

    [DataField("shardSlot2")]
    public string ShardSlotId2 = "shard2";

    [ViewVariables(VVAccess.ReadWrite)]
    public ItemSlot ShardSlot2 = default!;
}

[Serializable, NetSerializable]
public enum DungeonPortalVisualLayers : byte
{
    IsOpened,
    LeftPart,
    RightPart
}

[Serializable, NetSerializable]
public enum DungeonPortalVisuals : byte
{
    LeftState,
    RightState,
    OpenState
}
