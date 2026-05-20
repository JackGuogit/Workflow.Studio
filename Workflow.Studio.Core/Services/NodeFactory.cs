using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;

namespace Workflow.Studio.Core.Services;

public sealed class NodeFactory
{
    private readonly NodeManager _nodeManager;

    public NodeFactory(NodeManager nodeManager)
    {
        _nodeManager = nodeManager;
    }

    public IReadOnlyList<NodeDescriptor> GetAvailableNodes()
    {
        return _nodeManager.GetNodeDescriptors();
    }

    public NodeData CreateNode(string nodeTypeId, double x = 0, double y = 0, string? nodeId = null)
    {
        var definition = _nodeManager.GetNodeType(nodeTypeId);
        var resolvedNodeId = string.IsNullOrWhiteSpace(nodeId) ? $"node-{Guid.NewGuid():N}" : nodeId;
        return definition.CreateNode(resolvedNodeId, x, y);
    }
}
