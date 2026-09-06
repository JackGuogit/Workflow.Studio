using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowDocumentEditorTests
{
    [Fact]
    public void AddAndRemoveNode_CascadesConnections()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowDocumentEditor(document, registry);

        var source = editor.AddNode("source", 0, 0);
        var target = editor.AddNode("sink", 100, 0);

        Assert.True(editor.TryConnect(source.NodeId, "out", target.NodeId, "incoming", out var error));
        Assert.Null(error);
        Assert.Single(document.Connections);

        Assert.True(editor.RemoveNode(source.NodeId));

        Assert.Single(document.Nodes);
        Assert.Empty(document.Connections);
    }

    [Fact]
    public void Connect_ReplacesExistingIncomingConnection()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowDocumentEditor(document, registry);

        var sourceA = editor.AddNode("source", 0, 0);
        var sourceB = editor.AddNode("source", 100, 0);
        var target = editor.AddNode("sink", 200, 0);

        Assert.True(editor.TryConnect(sourceA.NodeId, "out", target.NodeId, "incoming", out _));
        Assert.True(editor.TryConnect(sourceB.NodeId, "out", target.NodeId, "incoming", out _));

        Assert.Single(document.Connections);
        Assert.Equal(sourceB.NodeId, document.Connections[0].SourceNodeId);
    }

    [Fact]
    public void Connect_RejectsTypeMismatchAndMissingPort()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowDocumentEditor(document, registry);

        var csv = editor.AddNode("csv-source", 0, 0);
        var sink = editor.AddNode("sink", 100, 0);

        Assert.False(editor.TryConnect(csv.NodeId, "out", sink.NodeId, "incoming", out var typeError));
        Assert.Contains("类型不兼容", typeError);

        Assert.False(editor.TryConnect(csv.NodeId, "missing", sink.NodeId, "incoming", out var portError));
        Assert.Contains("不存在", portError);
    }

    [Fact]
    public void Connect_RejectsCycle()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowDocumentEditor(document, registry);

        var nodeA = editor.AddNode("transform", 0, 0);
        var nodeB = editor.AddNode("transform", 100, 0);

        Assert.True(editor.TryConnect(nodeA.NodeId, "result", nodeB.NodeId, "incoming", out _));
        Assert.False(editor.TryConnect(nodeB.NodeId, "result", nodeA.NodeId, "incoming", out var error));
        Assert.Contains("环", error);
    }

    [Fact]
    public void Registry_ExposesDescriptorsForNodeLibrary()
    {
        var (_, registry) = CreateWorld();

        var descriptors = registry.Descriptors;

        Assert.Contains(descriptors, descriptor => descriptor.TypeId == "source");
        Assert.Contains(descriptors, descriptor => descriptor.DisplayName == "源节点");
    }

    private static (WorkflowDocument Document, WorkflowDefinitionRegistry Registry) CreateWorld()
    {
        var registry = new WorkflowDefinitionRegistry();
        registry.Register("source", new FakeSourceDefinition(), new NodeTypeDescriptor("source", "源节点", "Source"));
        registry.Register("csv-source", new FakeCsvSourceDefinition(), new NodeTypeDescriptor("csv-source", "CSV 源", "Source"));
        registry.Register("sink", new FakeSinkDefinition(), new NodeTypeDescriptor("sink", "汇点", "Output"));
        registry.Register("transform", new FakeTransformDefinition(), new NodeTypeDescriptor("transform", "变换", "Transform"));
        return (new WorkflowDocument(), registry);
    }

    private sealed class FakeSourceDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/plain" } };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = "x" }
            });
    }

    private sealed class FakeCsvSourceDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/csv")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/csv" } };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = "a,b" }
            });
    }

    private sealed class FakeSinkDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

        public IReadOnlyList<NodePortDefinition> OutputPorts => [];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?>() };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?>()
            });
    }

    private sealed class FakeTransformDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("result", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?> { ["result"] = "text/plain" } };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["result"] = "x" }
            });
    }
}
