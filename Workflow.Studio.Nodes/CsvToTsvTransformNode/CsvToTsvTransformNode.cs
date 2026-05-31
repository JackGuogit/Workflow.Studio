using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Nodes.Common;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Nodes;

public sealed class CsvToTsvTransformNode : WorkflowNodeDefinition<EmptyNodeSettings>
{
    private readonly CsvToTsvConverter _converter;

    public CsvToTsvTransformNode()
        : this(new CsvToTsvConverter())
    {
    }

    public CsvToTsvTransformNode(CsvToTsvConverter converter)
    {
        _converter = converter;
    }

    public const string TypeId = "demo.node.csv-to-tsv-transform";

    public override NodeDescriptor Descriptor { get; } = new()
    {
        NodeTypeId = TypeId,
        DisplayName = "CSV转TSV节点",
        Category = "Transform",
        Description = "将 CSV 文本解析后转换为 TSV 文本输出。"
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
        node.AddInputPort("incoming", "CSV输入", typeof(string), "Input", "上游传入的 CSV 文本");
        node.AddOutputPort("result", "TSV结果", typeof(string), "Output", "转换后的 TSV 文本");
    }

    protected override Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CoreExecutionContext executionContext,
        EmptyNodeSettings settings,
        CancellationToken cancellationToken)
    {
        request.InputValues.TryGetValue("incoming", out var incomingValue);
        var csvContent = Convert.ToString(incomingValue) ?? string.Empty;
        var tsvContent = _converter.Convert(csvContent);

        var result = new NodeExecutionResult
        {
            Message = "CSV 已转换为 TSV。"
        };

        result.OutputValues["result"] = tsvContent;
        result.GlobalVariables["LastTsvContent"] = tsvContent;
        return Task.FromResult(result);
    }
}
