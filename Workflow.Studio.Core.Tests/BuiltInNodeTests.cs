using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Nodes.BuiltIn;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class BuiltInNodeTests
{
    [Fact]
    public async Task TextChain_ExecutesAcrossBuiltInNodes()
    {
        var defs = new Dictionary<string, INodeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["demo.node.text-source"] = new TextSourceNode(),
            ["demo.node.uppercase-transform"] = new UppercaseTransformNode(),
            ["demo.node.preview"] = new PreviewNode()
        };

        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("source", "demo.node.text-source"));
        document.Nodes.Add(CreateNode("transform", "demo.node.uppercase-transform"));
        document.Nodes.Add(CreateNode("preview", "demo.node.preview"));
        document.Nodes[0].Settings["text"] = "hello";
        document.Connections.Add(Connect("source", "content", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "preview", "incoming"));

        var session = new WorkflowSession(document, typeId =>
            defs.TryGetValue(typeId, out var definition) ? definition : null!);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.True(session.GetNode("preview").TryReadOutputValue("preview-text", out var preview));
        Assert.Equal("HELLO", preview);
        Assert.Equal("HELLO", session.GetNode("transform").ProducedFlowVariables!["lastTransform"]);
    }

    [Fact]
    public async Task CsvChain_ReadConvertAndSave()
    {
        var tempCsv = Path.Combine(Path.GetTempPath(), $"ws-csv-{Guid.NewGuid():N}.csv");
        var tempTsv = Path.Combine(Path.GetTempPath(), $"ws-tsv-{Guid.NewGuid():N}.tsv");
        await File.WriteAllTextAsync(tempCsv, "a,b\n1,2\n");

        try
        {
            var defs = new Dictionary<string, INodeDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["demo.node.csv-read"] = new CsvReadNode(),
                ["demo.node.csv-to-tsv-transform"] = new CsvToTsvTransformNode(),
                ["demo.node.tsv-save"] = new TsvSaveNode()
            };

            var document = new WorkflowDocument();
            document.Nodes.Add(CreateNode("read", "demo.node.csv-read"));
            document.Nodes.Add(CreateNode("convert", "demo.node.csv-to-tsv-transform"));
            document.Nodes.Add(CreateNode("save", "demo.node.tsv-save"));
            document.Nodes[0].Settings["filePath"] = tempCsv;
            document.Nodes[2].Settings["filePath"] = tempTsv;
            document.Connections.Add(Connect("read", "csv-content", "convert", "incoming"));
            document.Connections.Add(Connect("convert", "result", "save", "incoming"));

            var session = new WorkflowSession(document, typeId =>
                defs.TryGetValue(typeId, out var definition) ? definition : null!);
            var executor = new WorkflowExecutor(session, maxConcurrency: 1);

            var result = await executor.ExecuteAllAsync();

            Assert.False(result.HasFailures);
            Assert.True(File.Exists(tempTsv));
            Assert.Equal($"a\tb{Environment.NewLine}1\t2", await File.ReadAllTextAsync(tempTsv));
            Assert.True(session.GetNode("save").TryReadOutputValue("saved-path", out var savedPath));
            Assert.Equal(tempTsv, savedPath);
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                File.Delete(tempCsv);
            }

            if (File.Exists(tempTsv))
            {
                File.Delete(tempTsv);
            }
        }
    }

    private static NodeDocument CreateNode(string nodeId, string nodeTypeId)
    {
        return new NodeDocument
        {
            NodeId = nodeId,
            NodeTypeId = nodeTypeId
        };
    }

    private static ConnectionDocument Connect(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
    {
        return new ConnectionDocument
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };
    }
}
