using Content.Shared.NodeContainer.NodeGroups;
using Content.Shared.Power;
using Robust.Shared.Maths;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Power;

[RegisterComponent]
public sealed partial class RandomPowerComponent : Component
{
    [DataField(required: true)]
    public string Node = string.Empty;

    [DataField]
    public string PipeLayerKey = "pipe_connector";

    [DataField]
    public Dictionary<NodeGroupID, Color> AvailableVoltages = new()
    {
        { NodeGroupID.HVPower, Color.FromHex("#666666") },
        { NodeGroupID.MVPower, Color.FromHex("#FFFFFF") },
        { NodeGroupID.Apc,     Color.FromHex("#33FF33") },
    };
}
