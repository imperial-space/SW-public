using System.Linq;
using Content.Shared.Examine;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;

namespace Content.Shared.Imperial.Medieval.Power;

public sealed partial class SharedMedievalPowerExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalPowerExaminableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<MedievalPowerExaminableComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<NodeContainerComponent>(ent.Owner, out var nodeContainer))
            return;

        if (nodeContainer.Nodes.TryGetValue(ent.Comp.ExaminableNode, out var node))
        {
            var text = GetLocalizedExamineText(node.NodeGroupID);

            if (!string.IsNullOrEmpty(text))
                args.PushMarkup(text);
        }
    }

    // todo fix loc
    public string GetLocalizedExamineText(NodeGroupID nodeGroupId)
    {
        return nodeGroupId switch
        {
            NodeGroupID.HVPower => Loc.GetString("Питание: смола"),

            NodeGroupID.MVPower => Loc.GetString("Питание: пар"),

            NodeGroupID.Apc => Loc.GetString("Питание: кислота"),

            _ => string.Empty
        };
    }
}
