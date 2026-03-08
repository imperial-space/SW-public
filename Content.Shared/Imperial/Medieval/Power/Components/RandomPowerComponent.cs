using Content.Shared.NodeContainer.NodeGroups;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Power;

[RegisterComponent]
public sealed partial class RandomPowerComponent : Component
{
    [DataField(required: true)]
    public string Node = string.Empty;

    [DataField]
    public Dictionary<NodeGroupID, string> AvailableVoltages = new()
    {
        { NodeGroupID.HVPower, "connector_hv" },
        { NodeGroupID.MVPower, "connector_mv" },
        { NodeGroupID.Apc,     "connector_lv" },
    };
}
