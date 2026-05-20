using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Plugins;

public sealed class PluginInitializationContext
{
}

public interface IWorkflowPlugin : IAsyncDisposable
{
    PluginMetadata Metadata { get; }

    ValueTask InitializeAsync(
        PluginInitializationContext context,
        CancellationToken cancellationToken);
}
