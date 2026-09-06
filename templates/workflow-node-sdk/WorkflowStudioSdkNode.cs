using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Session;

namespace WorkflowStudioSdkNode;

[WorkflowNodeType("sample.workflow-node", "工作流示例节点", "Sdk", "由 workflow-node 模板生成的示例节点。")]
public sealed class SampleNode : INodeDefinition
{
    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("incoming", "text/plain", IsOptional: true, DisplayName: "输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("result", "text/plain", DisplayName: "结果")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
        [new FlowVariableDeclaration("lastResult", "text/plain")];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["result"] = "text/plain" }
        };
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var input = request.InputValues.TryGetValue("incoming", out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        var suffix = request.Settings.TryGetValue("suffix", out var suffixValue) ? Convert.ToString(suffixValue) ?? string.Empty : string.Empty;
        var result = $"[{suffix}] {input}";

        return Task.FromResult(new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["result"] = result },
            OutputVariables = new Dictionary<string, object?> { ["lastResult"] = result }
        });
    }
}

public sealed class WorkflowStudioSdkNodeSettings
{
    [WorkflowSetting("后缀", "text")]
    public string Suffix { get; set; } = "processed";
}
