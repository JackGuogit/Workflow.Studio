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

public interface ITextTransformPlugin
{
    ValueTask<string> TransformToUppercaseAsync(string input, CancellationToken cancellationToken);
}

public interface IPreviewPlugin
{
    ValueTask<string> BuildPreviewAsync(string input, CancellationToken cancellationToken);
}
