using System.IO;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Nodes.TsvSave.View;
using Workflow.Studio.Nodes.TsvSave.ViewModels;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class TsvSaveNode : WorkflowNodeDefinition<TsvSaveNodeSettingsViewModel>
{
    public const string TypeId = "demo.node.tsv-save";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "TSV保存节点",
        Category = "Output",
        Description = "将 TSV 文本保存到文件系统。"
    };

    protected override TsvSaveNodeSettingsViewModel CreateSettingsCore()
    {
        return new TsvSaveNodeSettingsViewModel();
    }

    protected override Type? GetSettingsViewType()
    {
        return typeof(TsvSaveNodeSettingsView);
    }

    protected override void BuildNode(NodeData node, TsvSaveNodeSettingsViewModel settings)
    {
        node.AddInputPort("incoming", "TSV输入", typeof(string), "Input", "待保存的 TSV 文本");
        node.AddOutputPort("saved-path", "保存路径", typeof(string), "Output", "成功写入的文件路径");
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        TsvSaveNodeSettingsViewModel settings,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var tsvContent = Convert.ToString(incomingValue) ?? string.Empty;
        var filePath = ResolveRequiredPath(settings.FilePath);
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await File.WriteAllTextAsync(filePath, tsvContent, cancellationToken);

        var result = new NodeExecutionResult
        {
            Message = $"已保存 TSV 文件: {Path.GetFileName(filePath)}"
        };

        result.OutputValues["saved-path"] = filePath;
        result.GlobalVariables["LastTsvSavedPath"] = filePath;
        return result;
    }

    private static string ResolveRequiredPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("TSV 输出路径不能为空。");
        }

        return Path.GetFullPath(filePath);
    }
}
