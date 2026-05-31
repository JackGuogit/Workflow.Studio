using Workflow.Studio.Core.Models;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Core.Nodes;

public interface IWorkflowNodeDefinition
{
    NodeDescriptor Descriptor { get; }

    NodeData CreateNode(string nodeId, double x = 0, double y = 0);

    Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken);
}

public sealed class NodeDescriptor
{
    public required string NodeTypeId { get; init; }

    public required string DisplayName { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public abstract class WorkflowNodeDefinition<TSettings> : IWorkflowNodeDefinition
    where TSettings : class, INodeSettings
{
    public abstract NodeDescriptor Descriptor { get; }

    public NodeData CreateNode(string nodeId, double x = 0, double y = 0)
    {
        var settings = CreateSettingsCore();
        var node = new NodeData(
            new NodeMetadata
            {
                Id = nodeId,
                Name = Descriptor.DisplayName,
                Category = Descriptor.Category,
                Description = Descriptor.Description
            },
            Descriptor.NodeTypeId);

        node.Layout.X = x;
        node.Layout.Y = y;
        node.SetSettings(settings, GetSettingsViewType());
        BuildNode(node, settings);
        return node;
    }

    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(executionContext);

        return ExecuteAsync(
            request,
            executionContext,
            request.Node.GetSettings<TSettings>(),
            cancellationToken);
    }

    protected virtual Type? GetSettingsViewType()
    {
        return null;
    }

    protected abstract TSettings CreateSettingsCore();

    protected abstract void BuildNode(NodeData node, TSettings settings);

    protected abstract Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        TSettings settings,
        CancellationToken cancellationToken);
}
