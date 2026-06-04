using System.IO;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Nodes.OpenCvImageSave.View;
using Workflow.Studio.Nodes.OpenCvImageSave.ViewModels;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class OpenCvImageSaveNode : WorkflowNodeDefinition<OpenCvImageSaveNodeSettingsViewModel>
{
    private readonly IOpenCvImageCodecPlugin _imageCodecPlugin;

    public OpenCvImageSaveNode(IOpenCvImageCodecPlugin imageCodecPlugin)
    {
        _imageCodecPlugin = imageCodecPlugin;
    }

    public const string TypeId = "opencv.node.image-save";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "图片保存节点",
        Category = "OpenCV",
        Description = "将工作流图片帧保存到文件系统。"
    };

    protected override OpenCvImageSaveNodeSettingsViewModel CreateSettingsCore()
    {
        return new OpenCvImageSaveNodeSettingsViewModel();
    }

    protected override Type? GetSettingsViewType()
    {
        return typeof(OpenCvImageSaveNodeSettingsView);
    }

    protected override void BuildNode(NodeData node, OpenCvImageSaveNodeSettingsViewModel settings)
    {
        node.AddInputPort("image", "图片输入", typeof(WorkflowImageFrame), "Input", "待保存的图片帧", WorkflowPortSemanticTypes.ImageFrame);
        node.AddOutputPort("saved-path", "保存路径", typeof(string), "Output", "成功写入的文件路径", WorkflowPortSemanticTypes.FilePath);
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        OpenCvImageSaveNodeSettingsViewModel settings,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("image", out var incomingValue);
        var image = incomingValue as WorkflowImageFrame
            ?? throw new InvalidOperationException("图片保存节点未收到有效的图片输入。");

        var filePath = ResolveRequiredPath(settings.FilePath);
        await _imageCodecPlugin.SaveAsync(image, filePath, cancellationToken);

        var result = new NodeExecutionResult
        {
            Message = $"已保存图片文件: {Path.GetFileName(filePath)}"
        };

        result.OutputValues["saved-path"] = filePath;
        result.GlobalVariables["LastImageSavedPath"] = filePath;
        return result;
    }

    private static string ResolveRequiredPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("图片输出路径不能为空。");
        }

        return Path.GetFullPath(filePath);
    }
}
