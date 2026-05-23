using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Plugins;

namespace Workflow.Studio.Plugins.BuiltIn;

public sealed class UppercaseTransformPlugin : IWorkflowPlugin, ITextTransformPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "demo.uppercase-transform",
        Name = "Text Transform Extension",
        Description = "提供文本大写转换能力，供节点执行阶段调用。",
        Publisher = "Workflow Studio",
        Capabilities = ["TextTransform"]
    };

    public ValueTask InitializeAsync(
        PluginInitializationContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<string> TransformToUppercaseAsync(string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(input.ToUpperInvariant());
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class PreviewPlugin : IWorkflowPlugin, IPreviewPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "demo.preview",
        Name = "Preview Extension",
        Description = "提供预览内容生成能力，供节点执行阶段调用。",
        Publisher = "Workflow Studio",
        Capabilities = ["Preview"]
    };

    public ValueTask InitializeAsync(
        PluginInitializationContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<string> BuildPreviewAsync(string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(input);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
