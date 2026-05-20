using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Plugins.BuiltIn;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Core.Nodes.BuiltIn;

public sealed class TextSourceNode : IWorkflowNodeDefinition
{
    public const string TypeId = "demo.node.text-source";

    public NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "输入节点",
        Category = "Source",
        Description = "独立节点实现，负责生成初始文本输出。"
    };

    public NodeData CreateNode(string nodeId, double x = 0, double y = 0)
    {
        var node = new NodeData(
            new NodeMetadata
            {
                Id = nodeId,
                Name = Descriptor.DisplayName,
                Category = Descriptor.Category,
                Description = Descriptor.Description
            },
            TypeId);

        node.Layout.X = x;
        node.Layout.Y = y;
        node.Parameters["text"] = "Build workflow apps with WPF";
        node.Parameters["SeedText"] = new Dictionary<string, object>();
        node.AddOutputPort("content", "内容", typeof(string), "Output", "节点输出内容");
        node.AddOutputPort("content1", "内容", typeof(string), "Output", "节点输出内容");
        return node;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        executionContext.GlobalVariables.TryGetValue("SeedText", out var seedText);

        var text = request.Node.Parameters.TryGetValue("text", out var configuredText)
            ? Convert.ToString(configuredText)
            : Convert.ToString(seedText);

        await Task.Delay(5000, cancellationToken); // 模拟异步操作

        var result = new NodeExecutionResult
        {
            Message = "输入节点已生成输出内容。"
        };

        result.OutputValues["content"] = text ?? string.Empty;
        return result;
    }
}

public sealed class UppercaseTransformNode : IWorkflowNodeDefinition
{
    private readonly ITextTransformPlugin _textTransformPlugin;

    public UppercaseTransformNode(ITextTransformPlugin textTransformPlugin)
    {
        _textTransformPlugin = textTransformPlugin;
    }

    public const string TypeId = "demo.node.uppercase-transform";

    public NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "转换节点",
        Category = "Transform",
        Description = "独立节点实现，通过插件能力完成大写转换。"
    };

    public NodeData CreateNode(string nodeId, double x = 0, double y = 0)
    {
        var node = new NodeData(
            new NodeMetadata
            {
                Id = nodeId,
                Name = Descriptor.DisplayName,
                Category = Descriptor.Category,
                Description = Descriptor.Description
            },
            TypeId);

        node.Layout.X = x;
        node.Layout.Y = y;
        node.AddInputPort("incoming", "输入", typeof(string), "Input", "上游输入");
        node.AddOutputPort("result", "结果", typeof(string), "Output", "转换结果");
        return node;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var input = Convert.ToString(incomingValue) ?? string.Empty;
        var transformed = await _textTransformPlugin.TransformToUppercaseAsync(input, cancellationToken);
        var result = new NodeExecutionResult
        {
            Message = "转换节点已完成大写处理。"
        };

        result.OutputValues["result"] = transformed;
        result.GlobalVariables["LastTransform"] = transformed;
        return result;
    }
}

public sealed class PreviewNode : IWorkflowNodeDefinition
{
    private readonly IPreviewPlugin _previewPlugin;

    public PreviewNode(IPreviewPlugin previewPlugin)
    {
        _previewPlugin = previewPlugin;
    }

    public const string TypeId = "demo.node.preview";

    public NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "预览节点",
        Category = "Output",
        Description = "独立节点实现，通过插件能力生成预览内容。"
    };

    public NodeData CreateNode(string nodeId, double x = 0, double y = 0)
    {
        var node = new NodeData(
            new NodeMetadata
            {
                Id = nodeId,
                Name = Descriptor.DisplayName,
                Category = Descriptor.Category,
                Description = Descriptor.Description
            },
            TypeId);

        node.Layout.X = x;
        node.Layout.Y = y;
        node.AddInputPort("incoming", "输入", typeof(string), "Input", "预览输入");
        node.AddOutputPort("preview-text", "预览", typeof(string), "Output", "预览输出");
        return node;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var input = Convert.ToString(incomingValue) ?? string.Empty;
        var preview = await _previewPlugin.BuildPreviewAsync(input, cancellationToken);
        var result = new NodeExecutionResult
        {
            Message = "预览节点已生成预览内容。"
        };

        result.OutputValues["preview-text"] = preview;
        result.GlobalVariables["LastPreview"] = preview;
        return result;
    }
}
