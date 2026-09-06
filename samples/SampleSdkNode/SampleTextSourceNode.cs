using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Session;

namespace SampleSdkNode;

[Workflow.Studio.Core.Catalog.WorkflowNodeType("sample.text-source", "SDK 文本源", "Sdk", "外部加载示例节点")]
public sealed class SampleTextSourceNode : INodeDefinition
{
    public const string TypeId = "sample.text-source";

    public IReadOnlyList<NodePortDefinition> InputPorts => [];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("content", "text/plain", DisplayName: "内容")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
        [new FlowVariableDeclaration("sampleText", "text/plain")];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["content"] = "text/plain" }
        };
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var text = request.Settings.TryGetValue(nameof(SampleTextSourceSettings.Text), out var value)
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;

        return Task.FromResult(new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["content"] = text },
            OutputVariables = new Dictionary<string, object?> { ["sampleText"] = text }
        });
    }
}

public sealed class SampleTextSourceSettings
{
    public enum SampleMode
    {
        Default,
        Upper,
        Reverse
    }

    [WorkflowSetting("文本内容", "text")]
    public string Text { get; set; } = "来自 SDK 示例节点的文本";

    [WorkflowSetting("标签", "text")]
    public string Label { get; set; } = "demo";

    [WorkflowSetting("模式", "enum", typeof(SampleMode))]
    public SampleMode Mode { get; set; } = SampleMode.Default;
}

/// <summary>能力插件示例：与节点分离，供宿主插件目录管理。</summary>
[WorkflowPlugin("sample.capability", "Sample Capability", "示例能力插件。", new[] { "sample.transform" })]
public sealed class SampleCapabilityPlugin : IWorkflowPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "sample.capability",
        Name = "Sample Capability",
        Description = "示例能力插件。",
        Capabilities = ["sample.transform"]
    };

    public ValueTask InitializeAsync(PluginInitializationContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
