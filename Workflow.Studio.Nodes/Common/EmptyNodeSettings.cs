using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Nodes.Common;

public sealed class EmptyNodeSettings : INodeSettings
{
    public required string Title { get; init; }

    public required string Description { get; init; }
}
