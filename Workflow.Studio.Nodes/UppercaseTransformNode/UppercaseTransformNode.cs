using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Nodes.Common;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class UppercaseTransformNode : WorkflowNodeDefinition<EmptyNodeSettings>
{
    private readonly ITextTransformPlugin _textTransformPlugin;

    public UppercaseTransformNode(ITextTransformPlugin textTransformPlugin)
    {
        _textTransformPlugin = textTransformPlugin;
    }

    public const string TypeId = "demo.node.uppercase-transform";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "转换节点",
        Category = "Transform",
        Description = "独立节点实现，通过插件能力完成大写转换。"
    };

    protected override EmptyNodeSettings CreateSettingsCore()
    {
        return new EmptyNodeSettings
        {
            Title = Descriptor.DisplayName,
            Description = Descriptor.Description
        };
    }

    protected override void BuildNode(NodeData node, EmptyNodeSettings settings)
    {
        node.AddInputPort("incoming", "输入", typeof(string), "Input", "上游输入", WorkflowPortSemanticTypes.PlainText);
        node.AddOutputPort("result", "结果", typeof(string), "Output", "转换结果", WorkflowPortSemanticTypes.PlainText);
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        EmptyNodeSettings settings,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var input = Convert.ToString(incomingValue) ?? string.Empty;
        var transformed = await _textTransformPlugin.TransformToUppercaseAsync(input, cancellationToken);
        var result = new NodeExecutionResult
        {
            Message = "转换节点已完成大写处理。"
        };
        await Task.Delay(2000, cancellationToken);
        result.OutputValues["result"] = transformed;
        result.GlobalVariables["LastTransform"] = transformed;
        return result;
    }
}
