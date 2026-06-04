using System.IO;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Nodes.OpenCvImageRead.View;
using Workflow.Studio.Nodes.OpenCvImageRead.ViewModels;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class OpenCvImageReadNode : WorkflowNodeDefinition<OpenCvImageReadNodeSettingsViewModel>
{
    private readonly IOpenCvImageCodecPlugin _imageCodecPlugin;

    public OpenCvImageReadNode(IOpenCvImageCodecPlugin imageCodecPlugin)
    {
        _imageCodecPlugin = imageCodecPlugin;
    }

    public const string TypeId = "opencv.node.image-read";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "图片读取节点",
        Category = "OpenCV",
        Description = "从文件系统读取图片并输出工作流图片帧。"
    };

    protected override OpenCvImageReadNodeSettingsViewModel CreateSettingsCore()
    {
        return new OpenCvImageReadNodeSettingsViewModel();
    }

    protected override Type? GetSettingsViewType()
    {
        return typeof(OpenCvImageReadNodeSettingsView);
    }

    protected override void BuildNode(NodeData node, OpenCvImageReadNodeSettingsViewModel settings)
    {
        node.AddOutputPort("image", "图片", typeof(WorkflowImageFrame), "Output", "读取到的图片帧", WorkflowPortSemanticTypes.ImageFrame);
        node.AddOutputPort("source-path", "源路径", typeof(string), "Output", "实际读取的文件路径", WorkflowPortSemanticTypes.FilePath);
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        OpenCvImageReadNodeSettingsViewModel settings,
        CancellationToken cancellationToken)
    {
        var filePath = ResolveRequiredPath(settings.FilePath);
        var image = await _imageCodecPlugin.LoadAsync(filePath, settings.ReadMode, cancellationToken);

        var result = new NodeExecutionResult
        {
            Message = $"已读取图片文件: {Path.GetFileName(filePath)} ({image.Width}x{image.Height})"
        };

        result.OutputValues["image"] = image;
        result.OutputValues["source-path"] = filePath;
        result.GlobalVariables["LastImageSourcePath"] = filePath;
        return result;
    }

    private static string ResolveRequiredPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("图片文件路径不能为空。");
        }

        return Path.GetFullPath(filePath);
    }
}
