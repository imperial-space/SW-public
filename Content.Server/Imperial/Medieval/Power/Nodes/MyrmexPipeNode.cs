using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Medieval.Power;

[DataDefinition]
public sealed partial class MyrmexPipeNode : Node
{
    /// <summary>
    /// If disabled, this cable device will never connect.
    /// </summary>
    /// <remarks>
    /// If you change this,
    /// you must manually call <see cref="NodeGroupSystem.QueueReflood"/> to update the node connections.
    /// </remarks>
    [DataField]
    public bool Enabled = true;

    public override bool Connectable(IEntityManager entMan, TransformComponent? xform = null)
    {
        if (!Enabled)
            return false;

        return base.Connectable(entMan, xform);
    }

    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var mapSystem = entMan.System<SharedMapSystem>();
        var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);

        if (!entMan.TryGetComponent<MyrmexPipeComponent>(Owner, out var pipeComp))
            yield break;

        var rotation = xform.Comp.LocalRotation.GetCardinalDir();
        var allowedDirections = GetAllowedDirections(pipeComp.PipeType, rotation);

        foreach (var (dir, node) in NodeHelpers.GetCardinalNeighborNodes(nodeQuery, gridEnt, gridIndex, mapSystem))
        {
            if (dir == Direction.Invalid)
                continue;

            if (node is MyrmexPipeNode otherPipe)
            {
                if (!allowedDirections.Contains(dir))
                    continue;

                if (!CanOtherPipeConnect(otherPipe, dir.GetOpposite(), entMan, xformQuery))
                    continue;

                yield return node;
            }
            else if (node is MyrmexDeviceNode)
            {
                if (allowedDirections.Contains(dir))
                    yield return node;
            }
        }

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, gridIndex, mapSystem))
        {
            if (node == this)
                continue;

            if (node is MyrmexDeviceNode && entMan.HasComponent<MyrmexPipeEndpointComponent>(node.Owner))
                yield return node;
        }
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

    private bool CanOtherPipeConnect(MyrmexPipeNode otherNode, Direction oppositeDir,
        IEntityManager entMan, EntityQuery<TransformComponent> xformQuery)
    {
        if (!entMan.TryGetComponent<MyrmexPipeComponent>(otherNode.Owner, out var otherPipeComp))
            return false;

        if (!xformQuery.TryGetComponent(otherNode.Owner, out var otherXform))
            return false;

        var otherRotation = otherXform.LocalRotation.GetCardinalDir();
        var otherAllowedDirs = GetAllowedDirections(otherPipeComp.PipeType, otherRotation);

        return otherAllowedDirs.Contains(oppositeDir);
    }
}
