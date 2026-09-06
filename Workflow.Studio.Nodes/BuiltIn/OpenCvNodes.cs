using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Session;
using WorkflowImageFrame = Workflow.Studio.Core.Models.WorkflowImageFrame;
using WorkflowImageReadMode = Workflow.Studio.Core.Models.WorkflowImageReadMode;
using WorkflowThresholdMode = Workflow.Studio.Core.Models.WorkflowThresholdMode;

namespace Workflow.Studio.Nodes.BuiltIn;

/// <summary>
/// 内置 OpenCV 节点的契约实现。
/// </summary>
public sealed class OpenCvImageReadNode : INodeDefinition
{
    private readonly IOpenCvImageCodecPlugin _imageCodecPlugin;

    public OpenCvImageReadNode(IOpenCvImageCodecPlugin imageCodecPlugin)
    {
        _imageCodecPlugin = imageCodecPlugin;
    }

    public IReadOnlyList<NodePortDefinition> InputPorts => [];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
    [
        new NodePortDefinition("image", "image/frame", DisplayName: "图片"),
        new NodePortDefinition("source-path", "path/file", DisplayName: "源路径")
    ];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?>
            {
                ["image"] = "image/frame",
                ["source-path"] = "path/file"
            }
        };
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var filePath = ResolveRequiredPath(
            SettingReader.GetString(request.Settings, "filePath", string.Empty),
            "图片文件路径不能为空。");
        var readMode = SettingReader.GetEnum(request.Settings, "readMode", WorkflowImageReadMode.Color);
        var image = await _imageCodecPlugin.LoadAsync(filePath, readMode, cancellationToken);

        return new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?>
            {
                ["image"] = image,
                ["source-path"] = filePath
            },
            Message = $"已读取图片文件: {Path.GetFileName(filePath)} ({image.Width}x{image.Height})"
        };
    }

    internal static string ResolveRequiredPath(string filePath, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException(emptyMessage);
        }

        return Path.GetFullPath(filePath);
    }
}

public sealed class OpenCvGrayscaleNode : INodeDefinition
{
    private readonly IOpenCvImageProcessingPlugin _imageProcessingPlugin;

    public OpenCvGrayscaleNode(IOpenCvImageProcessingPlugin imageProcessingPlugin)
    {
        _imageProcessingPlugin = imageProcessingPlugin;
    }

    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("image", "image/frame", DisplayName: "图片输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("result", "image/frame", DisplayName: "灰度结果")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["result"] = "image/frame" }
        };
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var image = OpenCvInputReader.AsImageFrame(request, "image", "灰度化节点未收到有效的图片输入。");
        var grayscaleImage = await _imageProcessingPlugin.ConvertToGrayscaleAsync(image, cancellationToken);

        return new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["result"] = grayscaleImage },
            Message = $"已完成灰度化处理: {grayscaleImage.Width}x{grayscaleImage.Height}"
        };
    }
}

public sealed class OpenCvThresholdNode : INodeDefinition
{
    private readonly IOpenCvImageProcessingPlugin _imageProcessingPlugin;

    public OpenCvThresholdNode(IOpenCvImageProcessingPlugin imageProcessingPlugin)
    {
        _imageProcessingPlugin = imageProcessingPlugin;
    }

    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("image", "image/frame", DisplayName: "图片输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("result", "image/frame", DisplayName: "二值结果")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["result"] = "image/frame" }
        };
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var image = OpenCvInputReader.AsImageFrame(request, "image", "二值化节点未收到有效的图片输入。");
        var thresholdValue = SettingReader.GetByte(request.Settings, "thresholdValue", 127);
        var maxValue = SettingReader.GetByte(request.Settings, "maxValue", 255);
        var mode = SettingReader.GetEnum(request.Settings, "thresholdMode", WorkflowThresholdMode.Binary);
        var autoConvert = SettingReader.GetBool(request.Settings, "autoConvertToGrayscale", true);

        var thresholdImage = await _imageProcessingPlugin.ApplyThresholdAsync(
            image,
            thresholdValue,
            maxValue,
            mode,
            autoConvert,
            cancellationToken);

        return new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["result"] = thresholdImage },
            Message = $"已完成二值化处理: 阈值={thresholdValue}, 模式={mode}"
        };
    }
}

public sealed class OpenCvImageSaveNode : INodeDefinition
{
    private readonly IOpenCvImageCodecPlugin _imageCodecPlugin;

    public OpenCvImageSaveNode(IOpenCvImageCodecPlugin imageCodecPlugin)
    {
        _imageCodecPlugin = imageCodecPlugin;
    }

    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("image", "image/frame", DisplayName: "图片输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("saved-path", "path/file", DisplayName: "保存路径")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
        [new FlowVariableDeclaration("lastImageSavedPath", "path/file")];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["saved-path"] = "path/file" }
        };
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var image = OpenCvInputReader.AsImageFrame(request, "image", "图片保存节点未收到有效的图片输入。");
        var filePath = OpenCvImageReadNode.ResolveRequiredPath(
            SettingReader.GetString(request.Settings, "filePath", string.Empty),
            "图片输出路径不能为空。");

        await _imageCodecPlugin.SaveAsync(image, filePath, cancellationToken);

        return new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["saved-path"] = filePath },
            OutputVariables = new Dictionary<string, object?> { ["lastImageSavedPath"] = filePath },
            Message = $"已保存图片文件: {Path.GetFileName(filePath)}"
        };
    }
}

internal static class OpenCvInputReader
{
    public static WorkflowImageFrame AsImageFrame(NodeExecutionRequest request, string portId, string errorMessage)
    {
        return request.InputValues.TryGetValue(portId, out var value) && value is WorkflowImageFrame frame
            ? frame
            : throw new InvalidOperationException(errorMessage);
    }
}
