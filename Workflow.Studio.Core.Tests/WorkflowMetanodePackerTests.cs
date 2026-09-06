using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowMetanodePackerTests
{
    [Fact]
    public async Task Pack_SelectedNode_CreatesBoundaryAndExecutesInsideContainer()
    {
        var capture = new PackCaptureDefinition();
        var registry = new WorkflowDefinitionRegistry();
        registry.Register("src", new PackSourceDefinition(), new NodeTypeDescriptor("src", "源"));
        registry.Register("tr", new PackTransformDefinition(), new NodeTypeDescriptor("tr", "变换"));
        registry.Register("cap", capture, new NodeTypeDescriptor("cap", "汇点"));

        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("source", "src"));
        document.Nodes.Add(CreateNode("transform", "tr"));
        document.Nodes.Add(CreateNode("sink", "cap"));
        document.Nodes[0].Settings["text"] = "hello";
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));

        var meta = WorkflowMetanodePacker.Pack(document, registry, ["transform"]);

        Assert.Equal("meta", meta.NodeId.Substring(0, 4));
        Assert.Equal(3, document.Nodes.Count);
        Assert.Equal(2, document.Connections.Count);
        Assert.NotNull(meta.InnerWorkflow);
        Assert.Equal(3, meta.InnerWorkflow!.Nodes.Count);
        Assert.Equal(2, meta.InnerWorkflow.Nodes.Count(node =>
            node.NodeTypeId is ContainerTypeIds.BoundaryIn or ContainerTypeIds.BoundaryOut));

        var session = new WorkflowSession(document, typeId =>
            registry.TryResolve(typeId) ?? null!);
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal("hello|T", capture.LastInput);
        Assert.Equal(NodeState.Succeeded, session.GetNode(meta.NodeId).State);
    }

    [Fact]
    public async Task Unpack_RestoresOriginalTopologyAndExecutes()
    {
        var capture = new PackCaptureDefinition();
        var registry = new WorkflowDefinitionRegistry();
        registry.Register("src", new PackSourceDefinition(), new NodeTypeDescriptor("src", "源"));
        registry.Register("tr", new PackTransformDefinition(), new NodeTypeDescriptor("tr", "变换"));
        registry.Register("cap", capture, new NodeTypeDescriptor("cap", "汇点"));

        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("source", "src"));
        document.Nodes.Add(CreateNode("transform", "tr"));
        document.Nodes.Add(CreateNode("sink", "cap"));
        document.Nodes[0].Settings["text"] = "hello";
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));

        var meta = WorkflowMetanodePacker.Pack(document, registry, ["transform"]);
        Assert.True(WorkflowMetanodeUnpacker.Unpack(document, meta.NodeId));

        Assert.Equal(3, document.Nodes.Count);
        Assert.Equal(2, document.Connections.Count);
        Assert.DoesNotContain(document.Nodes, node => node.InnerWorkflow is not null);

        var session = new WorkflowSession(document, typeId =>
            registry.TryResolve(typeId) ?? null!);
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);
        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal("hello|T", capture.LastInput);
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

    private sealed class PackSourceDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/plain" } };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var text = request.Settings.TryGetValue("text", out var value) ? Convert.ToString(value) : string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = text ?? string.Empty }
            });
        }
    }

    private sealed class PackTransformDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("result", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?> { ["result"] = "text/plain" } };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var input = request.InputValues.TryGetValue("incoming", out var value) ? Convert.ToString(value) : string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["result"] = $"{input}|T" }
            });
        }
    }

    private sealed class PackCaptureDefinition : INodeDefinition
    {
        public string? LastInput { get; private set; }

        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

        public IReadOnlyList<NodePortDefinition> OutputPorts => [];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?>() };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            LastInput = request.InputValues.TryGetValue("incoming", out var value) ? Convert.ToString(value) : null;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?>()
            });
        }
    }
}
