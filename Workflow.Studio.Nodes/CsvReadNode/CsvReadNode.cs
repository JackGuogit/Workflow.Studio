using System.IO;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Nodes.CsvRead.View;
using Workflow.Studio.Nodes.CsvRead.ViewModels;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class CsvReadNode : WorkflowNodeDefinition<CsvReadNodeSettingsViewModel>
{
    public const string TypeId = "demo.node.csv-read";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "CSV读取节点",
        Category = "Source",
        Description = "从文件系统读取 CSV 内容并输出文本。"
    };

    protected override CsvReadNodeSettingsViewModel CreateSettingsCore()
    {
        return new CsvReadNodeSettingsViewModel();
    }

    protected override Type? GetSettingsViewType()
    {
        return typeof(CsvReadNodeSettingsView);
    }

    protected override void BuildNode(NodeData node, CsvReadNodeSettingsViewModel settings)
    {
        node.AddOutputPort("csv-content", "CSV内容", typeof(string), "Output", "读取到的 CSV 文本", WorkflowPortSemanticTypes.CsvText);
        node.AddOutputPort("source-path", "源路径", typeof(string), "Output", "实际读取的文件路径", WorkflowPortSemanticTypes.FilePath);
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CsvReadNodeSettingsViewModel settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var filePath = ResolveRequiredPath(settings.FilePath);
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        var result = new NodeExecutionResult
        {
            Message = $"已读取 CSV 文件: {Path.GetFileName(filePath)}"
        };

        result.OutputValues["csv-content"] = content;
        result.OutputValues["source-path"] = filePath;
        result.GlobalVariables["LastCsvSourcePath"] = filePath;
        return result;
    }

    private static string ResolveRequiredPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("CSV 文件路径不能为空。");
        }

        return Path.GetFullPath(filePath);
    }
}
