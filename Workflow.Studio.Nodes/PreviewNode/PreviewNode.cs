using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Nodes.Common;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class PreviewNode : WorkflowNodeDefinition<EmptyNodeSettings>
{
    private readonly IPreviewPlugin _previewPlugin;

    public PreviewNode(IPreviewPlugin previewPlugin)
    {
        _previewPlugin = previewPlugin;
    }

    public const string TypeId = "demo.node.preview";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "预览节点",
        Category = "Output",
        Description = "独立节点实现，通过插件能力生成预览内容。"
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
        node.AddInputPort("incoming", "输入", typeof(string), "Input", "预览输入");
        node.AddOutputPort("preview-text", "预览", typeof(string), "Output", "预览输出");
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        EmptyNodeSettings settings,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var input = Convert.ToString(incomingValue) ?? string.Empty;
        var preview = await _previewPlugin.BuildPreviewAsync(input, cancellationToken);
        var result = new NodeExecutionResult
        {
            Message = "预览节点已生成预览内容。"
        };
        await Task.Delay(2000, cancellationToken);
        result.OutputValues["preview-text"] = preview;
        result.GlobalVariables["LastPreview"] = preview;
        return result;
    }
}
