using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Services;

public sealed class NodeManager
{
    private readonly Dictionary<string, NodeData> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IWorkflowNodeDefinition> _nodeTypes = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterType(IWorkflowNodeDefinition nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);

        if (_nodeTypes.ContainsKey(nodeType.Descriptor.NodeTypeId))
        {
            throw new InvalidOperationException($"Node type '{nodeType.Descriptor.NodeTypeId}' is already registered.");
        }

        _nodeTypes[nodeType.Descriptor.NodeTypeId] = nodeType;
    }

    public void AttachWorkflow(WorkflowData workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        _nodes.Clear();

        foreach (var node in workflow.Nodes)
        {
            _nodes[node.Metadata.Id] = node;
        }
    }

    public NodeData GetNode(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            return node;
        }

        throw new KeyNotFoundException($"Node '{nodeId}' was not found.");
    }

    public IWorkflowNodeDefinition GetNodeType(string nodeTypeId)
    {
        if (_nodeTypes.TryGetValue(nodeTypeId, out var nodeType))
        {
            return nodeType;
        }

        throw new KeyNotFoundException($"Node type '{nodeTypeId}' was not found.");
    }

    public IReadOnlyList<NodeDescriptor> GetNodeDescriptors()
    {
        return _nodeTypes.Values
            .Select(nodeType => nodeType.Descriptor)
            .OrderBy(descriptor => descriptor.Category)
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public PortData GetPort(string nodeId, string portId)
    {
        var node = GetNode(nodeId);
        return node.FindPort(portId) ?? throw new KeyNotFoundException($"Port '{nodeId}.{portId}' was not found.");
    }

    public IReadOnlyList<ConnectionData> GetIncomingConnections(WorkflowData workflow, string nodeId)
    {
        return workflow.Connections.Where(connection => string.Equals(connection.TargetNodeId, nodeId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public IReadOnlyList<ConnectionData> GetOutgoingConnections(WorkflowData workflow, string nodeId)
    {
        return workflow.Connections.Where(connection => string.Equals(connection.SourceNodeId, nodeId, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
