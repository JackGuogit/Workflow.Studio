using System.Reflection;
using System.Text.Json;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Services;

public interface IWorkflowPersistenceService
{
    Task SaveAsync(WorkflowData workflow, string filePath, CancellationToken cancellationToken = default);

    Task<WorkflowData> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed class WorkflowPersistenceService : IWorkflowPersistenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly NodeFactory _nodeFactory;
    private readonly IWorkflowConnectionValidator _connectionValidator;

    public WorkflowPersistenceService(NodeFactory nodeFactory, IWorkflowConnectionValidator connectionValidator)
    {
        _nodeFactory = nodeFactory;
        _connectionValidator = connectionValidator;
    }

    public async Task SaveAsync(WorkflowData workflow, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var document = CreateDocument(workflow);
        _connectionValidator.EnsureWorkflowIsValid(workflow);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
    }

    public async Task<WorkflowData> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = File.OpenRead(filePath);
        var document = await JsonSerializer.DeserializeAsync<WorkflowDocument>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("无法读取工作流文件内容。");

        return RestoreWorkflow(document);
    }

    private WorkflowDocument CreateDocument(WorkflowData workflow)
    {
        return new WorkflowDocument
        {
            Version = 1,
            Nodes = workflow.Nodes.Select(CreateNodeDocument).ToList(),
            Connections = workflow.Connections.Select(connection => new WorkflowConnectionDocument
            {
                SourceNodeId = connection.SourceNodeId,
                SourcePortId = connection.SourcePortId,
                TargetNodeId = connection.TargetNodeId,
                TargetPortId = connection.TargetPortId
            }).ToList(),
            GlobalVariables = workflow.GlobalVariables.ToDictionary(
                entry => entry.Key,
                entry => CreateValueDocument(entry.Value, entry.Value?.GetType() ?? typeof(object)),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static WorkflowNodeDocument CreateNodeDocument(NodeData node)
    {
        return new WorkflowNodeDocument
        {
            NodeId = node.Metadata.Id,
            NodeTypeId = node.NodeTypeId,
            X = node.Layout.X,
            Y = node.Layout.Y,
            IsBreakpointEnabled = node.IsBreakpointEnabled,
            Settings = CreateSettingsDocument(node.Settings)
        };
    }

    private static Dictionary<string, WorkflowValueDocument> CreateSettingsDocument(INodeSettings? settings)
    {
        if (settings is null)
        {
            return new Dictionary<string, WorkflowValueDocument>(StringComparer.OrdinalIgnoreCase);
        }

        return GetPersistedProperties(settings.GetType())
            .ToDictionary(
                property => property.Name,
                property => CreateValueDocument(property.GetValue(settings), property.PropertyType),
                StringComparer.OrdinalIgnoreCase);
    }

    private WorkflowData RestoreWorkflow(WorkflowDocument document)
    {
        if (document.Version != 1)
        {
            throw new InvalidOperationException($"不支持的工作流文件版本: {document.Version}。");
        }

        var workflow = new WorkflowData();

        foreach (var nodeDocument in document.Nodes)
        {
            var node = _nodeFactory.CreateNode(nodeDocument.NodeTypeId, nodeDocument.X, nodeDocument.Y, nodeDocument.NodeId);
            ApplySettings(node, nodeDocument.Settings);
            node.SetBreakpointEnabled(nodeDocument.IsBreakpointEnabled);
            workflow.AddNode(node);
        }

        foreach (var connectionDocument in document.Connections)
        {
            var sourceNode = workflow.Nodes.FirstOrDefault(node => string.Equals(node.Metadata.Id, connectionDocument.SourceNodeId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"无法找到源节点 '{connectionDocument.SourceNodeId}'。");
            var targetNode = workflow.Nodes.FirstOrDefault(node => string.Equals(node.Metadata.Id, connectionDocument.TargetNodeId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"无法找到目标节点 '{connectionDocument.TargetNodeId}'。");

            _ = sourceNode.FindPort(connectionDocument.SourcePortId)
                ?? throw new InvalidOperationException($"无法找到源端口 '{connectionDocument.SourceNodeId}.{connectionDocument.SourcePortId}'。");
            _ = targetNode.FindPort(connectionDocument.TargetPortId)
                ?? throw new InvalidOperationException($"无法找到目标端口 '{connectionDocument.TargetNodeId}.{connectionDocument.TargetPortId}'。");

            workflow.Connect(
                connectionDocument.SourceNodeId,
                connectionDocument.SourcePortId,
                connectionDocument.TargetNodeId,
                connectionDocument.TargetPortId);
        }

        workflow.GlobalVariables.Clear();
        foreach (var globalVariable in document.GlobalVariables)
        {
            workflow.GlobalVariables[globalVariable.Key] = DeserializeValue(globalVariable.Value);
        }

        _connectionValidator.EnsureWorkflowIsValid(workflow);
        return workflow;
    }

    private static void ApplySettings(NodeData node, IReadOnlyDictionary<string, WorkflowValueDocument> values)
    {
        if (node.Settings is null || values.Count == 0)
        {
            return;
        }

        foreach (var property in GetPersistedProperties(node.Settings.GetType()))
        {
            if (!values.TryGetValue(property.Name, out var valueDocument))
            {
                continue;
            }

            var value = DeserializeValue(valueDocument, property.PropertyType);
            property.SetValue(node.Settings, value);
        }
    }

    private static IEnumerable<PropertyInfo> GetPersistedProperties(Type settingsType)
    {
        return settingsType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead
                && property.CanWrite
                && property.GetIndexParameters().Length == 0
                && !string.Equals(property.Name, nameof(INodeSettings.Title), StringComparison.Ordinal)
                && !string.Equals(property.Name, nameof(INodeSettings.Description), StringComparison.Ordinal));
    }

    private static WorkflowValueDocument CreateValueDocument(object? value, Type declaredType)
    {
        return new WorkflowValueDocument
        {
            TypeName = value?.GetType().AssemblyQualifiedName ?? declaredType.AssemblyQualifiedName,
            Value = JsonSerializer.SerializeToElement(value, declaredType, SerializerOptions)
        };
    }

    private static object? DeserializeValue(WorkflowValueDocument valueDocument, Type? fallbackType = null)
    {
        var resolvedType = ResolveType(valueDocument.TypeName, fallbackType);

        if (resolvedType is not null)
        {
            return valueDocument.Value.Deserialize(resolvedType, SerializerOptions);
        }

        return valueDocument.Value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => valueDocument.Value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when valueDocument.Value.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number when valueDocument.Value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => valueDocument.Value.GetDouble(),
            _ => valueDocument.Value.Clone()
        };
    }

    private static Type? ResolveType(string? typeName, Type? fallbackType)
    {
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var resolvedType = Type.GetType(typeName, throwOnError: false);
            if (resolvedType is not null)
            {
                return resolvedType;
            }
        }

        return fallbackType;
    }

    private sealed class WorkflowDocument
    {
        public int Version { get; init; }

        public List<WorkflowNodeDocument> Nodes { get; init; } = [];

        public List<WorkflowConnectionDocument> Connections { get; init; } = [];

        public Dictionary<string, WorkflowValueDocument> GlobalVariables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class WorkflowNodeDocument
    {
        public string NodeId { get; init; } = string.Empty;

        public string NodeTypeId { get; init; } = string.Empty;

        public double X { get; init; }

        public double Y { get; init; }

        public bool IsBreakpointEnabled { get; init; }

        public Dictionary<string, WorkflowValueDocument> Settings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class WorkflowConnectionDocument
    {
        public string SourceNodeId { get; init; } = string.Empty;

        public string SourcePortId { get; init; } = string.Empty;

        public string TargetNodeId { get; init; } = string.Empty;

        public string TargetPortId { get; init; } = string.Empty;
    }

    private sealed class WorkflowValueDocument
    {
        public string? TypeName { get; init; }

        public JsonElement Value { get; init; }
    }
}
