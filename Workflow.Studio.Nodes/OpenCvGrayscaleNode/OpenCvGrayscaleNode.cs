using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Nodes.Common;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class OpenCvGrayscaleNode : WorkflowNodeDefinition<EmptyNodeSettings>
{
    private readonly IOpenCvImageProcessingPlugin _imageProcessingPlugin;

    public OpenCvGrayscaleNode(IOpenCvImageProcessingPlugin imageProcessingPlugin)
    {
        _imageProcessingPlugin = imageProcessingPlugin;
    }

    public const string TypeId = "opencv.node.grayscale";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "灰度化节点",
        Category = "OpenCV",
        Description = "将彩色图片转换为灰度图。"
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
        node.AddInputPort("image", "图片输入", typeof(WorkflowImageFrame), "Input", "待灰度化的图片帧", WorkflowPortSemanticTypes.ImageFrame);
        node.AddOutputPort("result", "灰度结果", typeof(WorkflowImageFrame), "Output", "灰度化结果图片帧", WorkflowPortSemanticTypes.ImageFrame);
    }

    protected override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        EmptyNodeSettings settings,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("image", out var incomingValue);
        var image = incomingValue as WorkflowImageFrame
            ?? throw new InvalidOperationException("灰度化节点未收到有效的图片输入。");

        var grayscaleImage = await _imageProcessingPlugin.ConvertToGrayscaleAsync(image, cancellationToken);
        var result = new NodeExecutionResult
        {
            Message = $"已完成灰度化处理: {grayscaleImage.Width}x{grayscaleImage.Height}"
        };

        result.OutputValues["result"] = grayscaleImage;
        return result;
    }
}
