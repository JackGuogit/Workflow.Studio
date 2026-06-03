using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Services;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowConnectionValidatorTests
{
    private readonly WorkflowConnectionValidator _validator = new();

    [Fact]
    public void ValidateConnection_ShouldRejectDifferentSemanticTypes()
    {
        var workflow = new WorkflowData();
        var sourceNode = CreateNode("source", "Source");
        var targetNode = CreateNode("target", "Target");

        var output = sourceNode.AddOutputPort("csv", "CSV", typeof(string), semanticTypeKey: WorkflowPortSemanticTypes.CsvText);
        var input = targetNode.AddInputPort("tsv", "TSV", typeof(string), semanticTypeKey: WorkflowPortSemanticTypes.TsvText);

        workflow.AddNode(sourceNode);
        workflow.AddNode(targetNode);

        var result = _validator.ValidateConnection(workflow, sourceNode, output, targetNode, input);

        Assert.False(result.IsValid);
        Assert.Contains("端口类型不兼容", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateConnection_ShouldRejectMultipleIncomingConnections()
    {
        var workflow = new WorkflowData();
        var sourceA = CreateNode("source-a", "SourceA");
        var sourceB = CreateNode("source-b", "SourceB");
        var target = CreateNode("target", "Target");

        sourceA.AddOutputPort("out", "输出", typeof(string), semanticTypeKey: WorkflowPortSemanticTypes.PlainText);
        sourceB.AddOutputPort("out", "输出", typeof(string), semanticTypeKey: WorkflowPortSemanticTypes.PlainText);
        target.AddInputPort("in", "输入", typeof(string), semanticTypeKey: WorkflowPortSemanticTypes.PlainText);

        workflow.AddNode(sourceA);
        workflow.AddNode(sourceB);
        workflow.AddNode(target);
        workflow.Connect("source-a", "out", "target", "in");

        var result = _validator.ValidateConnection(
            workflow,
            sourceB,
            sourceB.FindPort("out")!,
            target,
            target.FindPort("in")!);

        Assert.False(result.IsValid);
        Assert.Contains("仅允许保留一条入线", result.Message, StringComparison.Ordinal);
    }

    private static NodeData CreateNode(string id, string name)
    {
        return new NodeData(
            new NodeMetadata
            {
                Id = id,
                Name = name,
                Category = "Test",
                Description = "Test node"
            },
            "test.node");
    }
}
