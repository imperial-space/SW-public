using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Magic.ActionOrder;

[RegisterComponent]
public sealed partial class MedievalActionUpgradeOrderComponent : Component
{
    public bool OrderInitialized;
    public bool OrderUpdateQueued;
    public bool ApplyingReplacements;
    public int RemovedOrderLimit = 16;
    public List<EntityUid> Actions = [];
    public HashSet<EntityUid> AvailableActions = [];
    public Dictionary<NetEntity, EntityUid> NetworkedActions = [];
    public Dictionary<NetEntity, NetEntity> Replacements = [];
    public Dictionary<NetEntity, MedievalRemovedActionOrder> RemovedOrders = [];
}

public sealed class MedievalRemovedActionOrder
{
    public List<EntityUid> Actions = [];
    public HashSet<EntityUid> AvailableActions = [];
    public Dictionary<NetEntity, EntityUid> NetworkedActions = [];
}
