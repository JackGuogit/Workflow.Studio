using System.IO;
using Workflow.Studio.Core.Documents;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowDocumentTests
{
    [Fact]
    public void RoundTrip_NestedDocumentWithMetanode_PreservesStructure()
    {
        var document = CreateNestedDocument();

        var json = WorkflowDocumentSerializer.Serialize(document);
        var loaded = WorkflowDocumentSerializer.Deserialize(json);

        Assert.Equal(WorkflowDocument.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(2, loaded.Nodes.Count);
        Assert.Single(loaded.Connections);
        Assert.Equal("in-1", loaded.Connections[0].TargetPortId);

        var declaration = Assert.Single(loaded.VariableDeclarations);
        Assert.Equal("seedText", declaration.Name);
        Assert.Equal("text/plain", declaration.TypeId);
        Assert.Equal("DevWorkflow Studio", declaration.DefaultValue);

        var source = loaded.Nodes.Single(node => node.NodeId == "node-source");
        Assert.Equal("abc", source.Settings["text"]);

        var meta = loaded.Nodes.Single(node => node.NodeId == "node-meta");
        Assert.Equal(ContainerTypeIds.MetaNode, meta.NodeTypeId);
        Assert.Equal(2, meta.VariableMappings.Count);
        Assert.Equal(VariableMappingDirection.In, meta.VariableMappings[0].Direction);
        Assert.Equal("seedText", meta.VariableMappings[0].Source);
        Assert.Equal("seed", meta.VariableMappings[0].Target);
        Assert.Equal("metaResult", meta.VariableMappings[1].Target);

        var inner = meta.InnerWorkflow
            ?? throw new InvalidOperationException("Expected metanode to carry an inner workflow.");
        Assert.Equal(3, inner.Nodes.Count);
        Assert.Equal(2, inner.Connections.Count);

        var inlet = inner.Nodes.Single(node => node.NodeId == "in-1");
        Assert.Equal(ContainerTypeIds.BoundaryIn, inlet.NodeTypeId);
        var inletPort = Assert.Single(inlet.Ports);
        Assert.Equal("out", inletPort.PortId);
        Assert.Equal("text/plain", inletPort.TypeId);

        var innerVariable = Assert.Single(inner.VariableDeclarations);
        Assert.Equal("seed", innerVariable.Name);
        Assert.Equal(string.Empty, innerVariable.DefaultValue);

        // 再次序列化应产出与原始文档一致的规范 JSON。
        Assert.Equal(json, WorkflowDocumentSerializer.Serialize(loaded));
    }

    [Fact]
    public async Task RoundTrip_FileSaveAndLoad_PreservesDocument()
    {
        var document = CreateNestedDocument();
        var tempPath = Path.Combine(Path.GetTempPath(), $"ws-v2-{Guid.NewGuid():N}.workflow.json");

        try
        {
            await WorkflowDocumentSerializer.SaveAsync(document, tempPath);
            var loaded = await WorkflowDocumentSerializer.LoadAsync(tempPath);

            Assert.Equal(document.Nodes.Count, loaded.Nodes.Count);
            Assert.Equal(document.Connections.Count, loaded.Connections.Count);
            Assert.Equal(
                WorkflowDocumentSerializer.Serialize(document),
                WorkflowDocumentSerializer.Serialize(loaded));
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void Deserialize_RejectsUnsupportedSchemaVersion()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "variableDeclarations": [],
              "nodes": [],
              "connections": []
            }
            """;

        Assert.Throws<InvalidOperationException>(() => WorkflowDocumentSerializer.Deserialize(json));
    }

    [Fact]
    public void Validate_RejectsDuplicateNodeIds()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("node-a", "demo.node.text-source"));
        document.Nodes.Add(CreateNode("NODE-A", "demo.node.preview"));

        var errors = WorkflowDocumentValidator.Validate(document);

        Assert.Contains(errors, error => error.Contains("duplicate node id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsDanglingConnection()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("node-a", "demo.node.text-source"));
        document.Connections.Add(new ConnectionDocument
        {
            SourceNodeId = "node-a",
            SourcePortId = "content",
            TargetNodeId = "missing-node",
            TargetPortId = "incoming"
        });

        var errors = WorkflowDocumentValidator.Validate(document);

        Assert.Contains(errors, error => error.Contains("missing-node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsBoundaryNodeWithoutPort()
    {
        var document = new WorkflowDocument();
        var meta = CreateMetaNode();

        var inner = meta.InnerWorkflow!;
        inner.Nodes.RemoveAll(node => node.NodeId == "in-1");
        inner.Nodes.Insert(0, new NodeDocument
        {
            NodeId = "in-1",
            NodeTypeId = ContainerTypeIds.BoundaryIn
        });

        document.Nodes.Add(meta);

        var errors = WorkflowDocumentValidator.Validate(document);

        Assert.Contains(errors, error => error.Contains("exactly one port", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Serialize_RejectsInvalidInMappingTarget()
    {
        var document = CreateNestedDocument();
        var meta = document.Nodes.Single(node => node.NodeId == "node-meta");

        meta.VariableMappings[0].Target = "not-declared";

        Assert.Throws<InvalidOperationException>(() => WorkflowDocumentSerializer.Serialize(document));
    }

    private static WorkflowDocument CreateNestedDocument()
    {
        var document = new WorkflowDocument();
        document.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seedText",
            TypeId = "text/plain",
            DefaultValue = "DevWorkflow Studio"
        });

        var source = CreateNode("node-source", "demo.node.text-source", x: 80, y: 120);
        source.Settings["text"] = "abc";
        document.Nodes.Add(source);

        var meta = CreateMetaNode();
        document.Nodes.Add(meta);

        document.Connections.Add(new ConnectionDocument
        {
            SourceNodeId = "node-source",
            SourcePortId = "content",
            TargetNodeId = "node-meta",
            TargetPortId = "in-1"
        });

        return document;
    }

    private static NodeDocument CreateMetaNode()
    {
        var inner = new WorkflowDocument();
        inner.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seed",
            TypeId = "text/plain",
            DefaultValue = string.Empty
        });

        inner.Nodes.Add(new NodeDocument
        {
            NodeId = "in-1",
            NodeTypeId = ContainerTypeIds.BoundaryIn,
            Ports =
            [
                new PortDocument { PortId = "out", TypeId = "text/plain" }
            ]
        });

        inner.Nodes.Add(CreateNode("inner-a", "demo.node.uppercase-transform", x: 160, y: 120));

        inner.Nodes.Add(new NodeDocument
        {
            NodeId = "out-1",
            NodeTypeId = ContainerTypeIds.BoundaryOut,
            Ports =
            [
                new PortDocument { PortId = "in", TypeId = "text/plain" }
            ]
        });

        inner.Connections.Add(new ConnectionDocument
        {
            SourceNodeId = "in-1",
            SourcePortId = "out",
            TargetNodeId = "inner-a",
            TargetPortId = "incoming"
        });

        inner.Connections.Add(new ConnectionDocument
        {
            SourceNodeId = "inner-a",
            SourcePortId = "result",
            TargetNodeId = "out-1",
            TargetPortId = "in"
        });

        return new NodeDocument
        {
            NodeId = "node-meta",
            NodeTypeId = ContainerTypeIds.MetaNode,
            X = 420,
            Y = 120,
            VariableMappings =
            [
                new VariableMapping
                {
                    Direction = VariableMappingDirection.In,
                    Source = "seedText",
                    Target = "seed"
                },
                new VariableMapping
                {
                    Direction = VariableMappingDirection.Out,
                    Source = "result",
                    Target = "metaResult"
                }
            ],
            InnerWorkflow = inner
        };
    }

    private static NodeDocument CreateNode(string nodeId, string nodeTypeId, double x = 0, double y = 0)
    {
        return new NodeDocument
        {
            NodeId = nodeId,
            NodeTypeId = nodeTypeId,
            X = x,
            Y = y
        };
    }
}
