using System.Collections.ObjectModel;

namespace Workflow.Studio.Core.Models;

public enum PortDirection
{
    Input,
    Output
}

public enum PortStatus
{
    Ready,
    Connected,
    HasData,
    Failed
}

public enum NodeStatus
{
    Ready,
    Running,
    Success,
    Failed
}

public interface IPort
{
    PortMetadata Metadata { get; }

    PortDirection Direction { get; }

    object? Value { get; }

    PortStatus Status { get; }
}

public interface INode
{
    NodeMetadata Metadata { get; }

    string NodeTypeId { get; }

    IReadOnlyCollection<PortData> InputPorts { get; }

    IReadOnlyCollection<PortData> OutputPorts { get; }
}

public sealed class PortMetadata
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Type DataType { get; init; }

    public string GroupName { get; init; } = "Default";

    public string Description { get; init; } = string.Empty;
}

public sealed class PortData : IPort
{
    public PortData(PortMetadata metadata, PortDirection direction)
    {
        Metadata = metadata;
        Direction = direction;
    }

    public PortMetadata Metadata { get; }

    public PortDirection Direction { get; }

    public object? Value { get; private set; }

    public PortStatus Status { get; private set; } = PortStatus.Ready;

    public bool IsCollapsed { get; private set; }

    public void SetValue(object? value)
    {
        Value = value;
        Status = value is null ? PortStatus.Connected : PortStatus.HasData;
    }

    public void MarkConnected()
    {
        if (Status == PortStatus.Ready)
        {
            Status = PortStatus.Connected;
        }
    }

    public void MarkFailed()
    {
        Status = PortStatus.Failed;
    }

    public void Clear()
    {
        Value = null;
        Status = PortStatus.Ready;
    }

    public void SetCollapsed(bool isCollapsed)
    {
        IsCollapsed = isCollapsed;
    }
}

public sealed class NodeMetadata
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public sealed class NodeLayoutData
{
    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class NodeData : INode
{
    public NodeData(NodeMetadata metadata, string nodeTypeId)
    {
        Metadata = metadata;
        NodeTypeId = nodeTypeId;
    }

    public NodeMetadata Metadata { get; }

    public string NodeTypeId { get; }

    public NodeStatus Status { get; private set; } = NodeStatus.Ready;

    public NodeLayoutData Layout { get; } = new();

    public IDictionary<string, object?> Parameters { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public Collection<PortData> InputPorts { get; } = [];

    public Collection<PortData> OutputPorts { get; } = [];

    IReadOnlyCollection<PortData> INode.InputPorts => InputPorts;

    IReadOnlyCollection<PortData> INode.OutputPorts => OutputPorts;

    public PortData AddInputPort(string id, string name, Type dataType, string groupName = "Input", string? description = null)
    {
        var port = new PortData(
            new PortMetadata
            {
                Id = id,
                Name = name,
                DataType = dataType,
                GroupName = groupName,
                Description = description ?? string.Empty
            },
            PortDirection.Input);

        InputPorts.Add(port);
        return port;
    }

    public PortData AddOutputPort(string id, string name, Type dataType, string groupName = "Output", string? description = null)
    {
        var port = new PortData(
            new PortMetadata
            {
                Id = id,
                Name = name,
                DataType = dataType,
                GroupName = groupName,
                Description = description ?? string.Empty
            },
            PortDirection.Output);

        OutputPorts.Add(port);
        return port;
    }

    public PortData? FindPort(string portId)
    {
        return InputPorts.Concat(OutputPorts).FirstOrDefault(port => string.Equals(port.Metadata.Id, portId, StringComparison.OrdinalIgnoreCase));
    }

    public void SetStatus(NodeStatus status)
    {
        Status = status;
    }
}

public sealed class ConnectionData
{
    public required string SourceNodeId { get; init; }

    public required string SourcePortId { get; init; }

    public required string TargetNodeId { get; init; }

    public required string TargetPortId { get; init; }
}

public sealed class WorkflowData
{
    public Collection<NodeData> Nodes { get; } = [];

    public Collection<ConnectionData> Connections { get; } = [];

    public IDictionary<string, object?> GlobalVariables { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public NodeData AddNode(NodeData node)
    {
        Nodes.Add(node);
        return node;
    }

    public ConnectionData Connect(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
    {
        var connection = new ConnectionData
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };

        Connections.Add(connection);
        return connection;
    }
}

public sealed class PluginMetadata
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Version { get; init; } = "1.0.0";

    public string Publisher { get; init; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed class NodeExecutionRequest
{
    public required NodeData Node { get; init; }

    public required IReadOnlyDictionary<string, object?> InputValues { get; init; }
}

public sealed class NodeExecutionResult
{
    public IDictionary<string, object?> OutputValues { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, object?> GlobalVariables { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public string? Message { get; init; }
}

public sealed class NodeExecutionRecord
{
    public required string NodeId { get; init; }

    public required string NodeName { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required NodeStatus Status { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyDictionary<string, object?> InputSnapshot { get; init; }

    public required IReadOnlyDictionary<string, object?> OutputSnapshot { get; init; }
}
