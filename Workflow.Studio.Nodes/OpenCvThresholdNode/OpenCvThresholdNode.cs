using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Nodes.OpenCvThreshold.View;
using Workflow.Studio.Nodes.OpenCvThreshold.ViewModels;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class OpenCvThresholdNode : WorkflowNodeDefinition<OpenCvThresholdNodeSettingsViewModel>
{
    private readonly IOpenCvImageProcessingPlugin _imageProcessingPlugin;

    public OpenCvThresholdNode(IOpenCvImageProcessingPlugin imageProcessingPlugin)
    {
        _imageProcessingPlugin = imageProcessingPlugin;
    }

    public const string TypeId = "opencv.node.threshold";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "二值化节点",
        Category = "OpenCV",
        Description = "对灰度图执行阈值二值化处理。"
    };

    protected override OpenCvThresholdNodeSettingsViewModel CreateSettingsCore()
    {
        return new OpenCvThresholdNodeSettingsViewModel();
    }

    protected override Type? GetSettingsViewType()
    {
        return typeof(OpenCvThresholdNodeSettingsView);
    }

    protected override void BuildNode(NodeData node, OpenCvThresholdNodeSettingsViewModel settings)
    {
        node.AddInputPort("image", "图片输入", typeof(WorkflowImageFrame), "Input", "待二值化的图片帧", WorkflowPortSemanticTypes.ImageFrame);
        node.AddOutputPort("result", "二值结果", typeof(WorkflowImageFrame), "Output", "二值化结果图片帧", WorkflowPortSemanticTypes.ImageFrame);
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        OpenCvThresholdNodeSettingsViewModel settings,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("image", out var incomingValue);
        var image = incomingValue as WorkflowImageFrame
            ?? throw new InvalidOperationException("二值化节点未收到有效的图片输入。");

        var thresholdImage = await _imageProcessingPlugin.ApplyThresholdAsync(
            image,
            settings.ThresholdValue,
            settings.MaxValue,
            settings.ThresholdMode,
            settings.AutoConvertToGrayscale,
            cancellationToken);

        var result = new NodeExecutionResult
        {
            Message = $"已完成二值化处理: 阈值={settings.ThresholdValue}, 模式={settings.ThresholdMode}"
        };

        result.OutputValues["result"] = thresholdImage;
        return result;
    }
}
