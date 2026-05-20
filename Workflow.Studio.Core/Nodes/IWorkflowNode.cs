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
