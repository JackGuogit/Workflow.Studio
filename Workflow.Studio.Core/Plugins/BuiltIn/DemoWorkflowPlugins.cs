using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes.BuiltIn;

namespace Workflow.Studio.Core.Plugins.BuiltIn;

public sealed class UppercaseTransformPlugin : IWorkflowPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "demo.uppercase-transform",
        Name = "Text Transform Extension",
        Description = "为工作流平台注册文本转换相关节点。",
        Publisher = "Workflow Studio",
        Capabilities = ["NodeRegistration", "TextTransform"]
    };

    public ValueTask InitializeAsync(
        PluginInitializationContext context,
        CancellationToken cancellationToken)
    {
        context.NodeManager.RegisterType(new UppercaseTransformNode());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class PreviewPlugin : IWorkflowPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "demo.preview",
        Name = "Preview Extension",
        Description = "为工作流平台注册预览输出相关节点。",
        Publisher = "Workflow Studio",
        Capabilities = ["NodeRegistration", "Preview"]
    };

    public ValueTask InitializeAsync(
        PluginInitializationContext context,
        CancellationToken cancellationToken)
    {
        context.NodeManager.RegisterType(new PreviewNode());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
