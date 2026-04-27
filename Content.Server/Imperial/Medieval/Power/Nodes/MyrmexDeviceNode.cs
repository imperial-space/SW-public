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

            if ((ConnectDirections == DirectionFlag.None || (ConnectDirections & dir.AsFlag()) != 0) 
                && node is MyrmexPipeNode pipeNode)
            {
                if (CanPipeConnectFromDirection(pipeNode, dir.GetOpposite(), entMan, xformQuery))
                    yield return node;
            }
        }


        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, gridIndex))
        {
            if (node != this && node is MyrmexPipeNode)
                yield return node;
        }
    }

    private bool CanPipeConnectFromDirection(MyrmexPipeNode pipeNode, Direction dirFromPipe,
        IEntityManager entMan, EntityQuery<TransformComponent> xformQuery)
    {
        if (!entMan.TryGetComponent<MyrmexPipeComponent>(pipeNode.Owner, out var pipeComp))
            return false;

        if (!xformQuery.TryGetComponent(pipeNode.Owner, out var pipeXform))
            return false;

        var pipeRotation = pipeXform.LocalRotation.GetCardinalDir();
        var allowedDirections = GetAllowedDirections(pipeComp.PipeType, pipeRotation);

        return allowedDirections.Contains(dirFromPipe);
    }

    private HashSet<Direction> GetAllowedDirections(MyrmexPipeType pipeType, Direction rotation)
    {
        var directions = new HashSet<Direction>();

        switch (pipeType)
        {
            case MyrmexPipeType.Straight:
                directions.Add(rotation);
                directions.Add(rotation.GetOpposite());
                break;

            case MyrmexPipeType.Corner:
                directions.Add(rotation);
                directions.Add(rotation.GetClockwise90Degrees());
                break;

            case MyrmexPipeType.TJunction:
                directions.Add(rotation);
                directions.Add(rotation.GetOpposite());
                directions.Add(rotation.GetClockwise90Degrees());
                break;

            case MyrmexPipeType.Cross:
                directions.Add(Direction.North);
                directions.Add(Direction.South);
                directions.Add(Direction.East);
                directions.Add(Direction.West);
                break;
        }

        return directions;
    }
}
