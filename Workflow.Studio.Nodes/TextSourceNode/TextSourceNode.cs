using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Nodes.TextSource.View;
using Workflow.Studio.Nodes.TextSource.ViewModels;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class TextSourceNode : WorkflowNodeDefinition<TextSourceNodeSettingsViewModel>
{
    public const string TypeId = "demo.node.text-source";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "输入节点",
        Category = "Source",
        Description = "独立节点实现，负责生成初始文本输出。"
    };

    protected override TextSourceNodeSettingsViewModel CreateSettingsCore()
    {
        return new TextSourceNodeSettingsViewModel();
    }

    protected override Type? GetSettingsViewType()
    {
        return typeof(TextSourceNodeSettingsView);
    }

    protected override void BuildNode(NodeData node, TextSourceNodeSettingsViewModel settings)
    {
        node.AddOutputPort("content", "内容", typeof(string), "Output", "节点输出内容", WorkflowPortSemanticTypes.PlainText);
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        TextSourceNodeSettingsViewModel settings,
        CancellationToken cancellationToken)
    {
        executionContext.GlobalVariables.TryGetValue("GlobalVariablesSeedText", out var seedText);
        var text = string.IsNullOrWhiteSpace(settings.Text)
            ? Convert.ToString(seedText)
            : settings.Text;

        await Task.Delay(5000, cancellationToken);

        var result = new NodeExecutionResult
        {
            Message = "输入节点已生成输出内容。"
        };

        result.OutputValues["content"] = text ?? string.Empty;
        return result;
    }
}
