using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Session;

namespace SampleSdkNode;

/// <summary>SDK 示例：设置 POCO 带 [WorkflowSetting] 元数据，执行逻辑只读字典设置。</summary>
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
    [WorkflowSetting("文本内容", "text")]
    public string Text { get; set; } = "来自 SDK 示例节点的文本";

    [WorkflowSetting("标签", "text")]
    public string Label { get; set; } = "demo";
}
