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

    Collection<PortData> InputPorts { get; }

    Collection<PortData> OutputPorts { get; }
}

public interface INodeSettings
{
    string Title { get; }

    string Description { get; }
}

public sealed class PortMetadata
{
    public string Id { get; init; }

    public string Name { get; init; }

    public Type DataType { get; init; }

    public string SemanticTypeKey { get; init; } = string.Empty;

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

    public void SetValue(object? value, string? context = null)
    {
        PortTypeCompatibility.EnsureValueMatches(
            Metadata.DataType,
            value,
            context ?? $"端口 '{Metadata.Name}'");

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
    public string Id { get; init; }

    public string Name { get; init; }

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
    private INodeSettings? _settings;

    public NodeData(NodeMetadata metadata, string nodeTypeId)
    {
        Metadata = metadata;
        NodeTypeId = nodeTypeId;
    }

    public NodeMetadata Metadata { get; }

    public string NodeTypeId { get; }

    public NodeStatus Status { get; private set; } = NodeStatus.Ready;

    public NodeLayoutData Layout { get; } = new();

    public INodeSettings? Settings => _settings;

    public Type? SettingsViewType { get; private set; }

    public Collection<PortData> InputPorts { get; } = [];

    public Collection<PortData> OutputPorts { get; } = [];

    public bool HasSettings => _settings is not null;

    public PortData AddInputPort(
        string id,
        string name,
        Type dataType,
        string groupName = "Input",
        string? description = null,
        string? semanticTypeKey = null)
    {
        var port = new PortData(
            new PortMetadata
            {
                Id = id,
                Name = name,
                DataType = dataType,
                SemanticTypeKey = semanticTypeKey ?? string.Empty,
                GroupName = groupName,
                Description = description ?? string.Empty
            },
            PortDirection.Input);

        InputPorts.Add(port);
        return port;
    }

    public PortData AddOutputPort(
        string id,
        string name,
        Type dataType,
        string groupName = "Output",
        string? description = null,
        string? semanticTypeKey = null)
    {
        var port = new PortData(
            new PortMetadata
            {
                Id = id,
                Name = name,
                DataType = dataType,
                SemanticTypeKey = semanticTypeKey ?? string.Empty,
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

    public TSettings GetSettings<TSettings>()
        where TSettings : class, INodeSettings
    {
        return _settings as TSettings
            ?? throw new InvalidOperationException($"Node '{Metadata.Id}' does not contain settings of type '{typeof(TSettings).FullName}'.");
    }

    public void SetSettings<TSettings>(TSettings settings, Type? settingsViewType = null)
        where TSettings : class, INodeSettings
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        SettingsViewType = settingsViewType;
    }

    public void SetStatus(NodeStatus status)
    {
        Status = status;
    }
}

public sealed class ConnectionData
{
    public string SourceNodeId { get; init; }

    public string SourcePortId { get; init; }

    public string TargetNodeId { get; init; }

    public string TargetPortId { get; init; }
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
    public string Id { get; init; }

    public string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Version { get; init; } = "1.0.0";

    public string Publisher { get; init; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed class NodeExecutionRequest
{
    public NodeData Node { get; init; }

    public IReadOnlyDictionary<string, object?> InputValues { get; init; }
}

public sealed class NodeExecutionResult
{
    public IDictionary<string, object?> OutputValues { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, object?> GlobalVariables { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public string? Message { get; init; }
}

public sealed class NodeExecutionRecord
{
    public string NodeId { get; init; }

    public string NodeName { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }

    public NodeStatus Status { get; init; }

    public string? Message { get; init; }

    public IReadOnlyDictionary<string, object?> InputSnapshot { get; init; }

    public IReadOnlyDictionary<string, object?> OutputSnapshot { get; init; }
}
