using Workflow.Studio.Core.Models;
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
        node.AddOutputPort("content", "内容", typeof(string), "Output", "节点输出内容");
        node.AddOutputPort("content1", "内容", typeof(string), "Output", "节点输出内容");
        return node;
    }

    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        executionContext.GlobalVariables.TryGetValue("SeedText", out var seedText);

        var text = request.Node.Parameters.TryGetValue("text", out var configuredText)
            ? Convert.ToString(configuredText)
            : Convert.ToString(seedText);

        var result = new NodeExecutionResult
        {
            Message = "输入节点已生成输出内容。"
        };

        result.OutputValues["content"] = text ?? string.Empty;
        return Task.FromResult(result);
    }
}

public sealed class UppercaseTransformNode : IWorkflowNodeDefinition
{
    public const string TypeId = "demo.node.uppercase-transform";

    public NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "转换节点",
        Category = "Transform",
        Description = "独立节点实现，内部通过插件接口调用大写转换能力。"
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

    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var input = Convert.ToString(incomingValue) ?? string.Empty;
        var transformed = input.ToUpperInvariant();
        var result = new NodeExecutionResult
        {
            Message = "转换节点已完成大写处理。"
        };

        result.OutputValues["result"] = transformed;
        result.GlobalVariables["LastTransform"] = transformed;

        return Task.FromResult(result);
    }
}

public sealed class PreviewNode : IWorkflowNodeDefinition
{
    public const string TypeId = "demo.node.preview";

    public NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "预览节点",
        Category = "Output",
        Description = "独立节点实现，内部通过插件接口调用预览能力。"
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

    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var input = Convert.ToString(incomingValue) ?? string.Empty;
        var result = new NodeExecutionResult
        {
            Message = "预览节点已生成预览内容。"
        };

        result.OutputValues["preview-text"] = input;
        result.GlobalVariables["LastPreview"] = input;

        return Task.FromResult(result);
    }
}
