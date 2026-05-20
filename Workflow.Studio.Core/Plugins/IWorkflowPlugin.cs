using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Services;

namespace Workflow.Studio.Core.Plugins;

public sealed class PluginInitializationContext
{
    public PluginInitializationContext(NodeManager nodeManager)
    {
        NodeManager = nodeManager;
    }

    public NodeManager NodeManager { get; }
}

public interface IWorkflowPlugin : IAsyncDisposable
{
    PluginMetadata Metadata { get; }

    ValueTask InitializeAsync(
        PluginInitializationContext context,
        CancellationToken cancellationToken);
}
