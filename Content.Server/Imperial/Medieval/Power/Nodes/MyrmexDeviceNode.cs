using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.Imperial.Medieval.Power;

[DataDefinition, Virtual]
public partial class MyrmexDeviceNode : Node
{
    [DataField]
    public DirectionFlag ConnectDirections = DirectionFlag.None;

    public override IEnumerable<Node> GetReachableNodes(
        TransformComponent xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        MapGridComponent? grid,
        IEntityManager entMan)
    {
        if (!xform.Anchored || grid == null)
            yield break;

        var gridIndex = grid.TileIndicesFor(xform.Coordinates);

        foreach (var (dir, node) in NodeHelpers.GetCardinalNeighborNodes(nodeQuery, grid, gridIndex))
        {
            if (dir == Direction.Invalid)
                continue;

            if ((ConnectDirections & dir.AsFlag()) != 0 && node is MyrmexPipeNode)
                yield return node;
        }


        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, gridIndex))
        {
            if (node != this && node is MyrmexPipeNode)
                yield return node;
        }
    }
}
