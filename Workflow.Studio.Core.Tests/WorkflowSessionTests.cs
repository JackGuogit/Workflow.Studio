using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowSessionTests
{
    [Fact]
    public void ConfigureAll_PropagatesSpecsAlongChain()
    {
        var (session, source, transform, _) = CreateChainSession();

        session.ConfigureAll();

        Assert.Equal(NodeState.Configured, source.State);
        Assert.Equal(NodeState.Configured, transform.State);
        Assert.Equal("spec:hello|x", transform.FindOutput("result")!.Spec);
    }

    [Fact]
    public void NotifyNodeChanged_ReconfiguresDownstreamAndKeepsUpstreamClean()
    {
        var (session, source, transform, sink) = CreateChainSession();
        session.ConfigureAll();

        var events = new List<(string NodeId, NodeState Previous, NodeState New)>();
        session.NodeStateChanged += (_, args) =>
            events.Add((args.NodeId, args.PreviousState, args.NewState));

        source.SourceDocument.Settings["text"] = "world";
        session.NotifyNodeChanged(source.NodeId);

        Assert.Equal(NodeState.Configured, source.State);
        Assert.Equal(NodeState.Configured, transform.State);
        Assert.Equal(NodeState.Configured, sink.State);
        Assert.Equal("spec:world|x", transform.FindOutput("result")!.Spec);

        Assert.Contains(events, e => e.NodeId == source.NodeId && e.Previous == NodeState.Configured && e.New == NodeState.NotConfigured);
        Assert.Contains(events, e => e.NodeId == transform.NodeId && e.Previous == NodeState.Configured && e.New == NodeState.NotConfigured);
        Assert.Contains(events, e => e.NodeId == sink.NodeId && e.Previous == NodeState.Configured && e.New == NodeState.NotConfigured);
        Assert.Contains(events, e => e.NodeId == sink.NodeId && e.Previous == NodeState.NotConfigured && e.New == NodeState.Configured);
        Assert.Contains(events, e => e.NodeId == source.NodeId && e.New == NodeState.Configured);
    }

    [Fact]
    public void UnconnectedRequiredInput_StaysNotConfiguredWithError()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("sink", "fake/sink"));
        var session = CreateSession(document);

        session.ConfigureAll();

        var sink = session.GetNode("sink");
        Assert.Equal(NodeState.NotConfigured, sink.State);
        Assert.Contains("必连输入", sink.LastError);
    }

    [Fact]
    public void ConfigureAll_CycleThrows()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("t1", "fake/transform"));
        document.Nodes.Add(CreateNode("t2", "fake/transform"));
        document.Connections.Add(Connect("t1", "result", "t2", "incoming"));
        document.Connections.Add(Connect("t2", "result", "t1", "incoming"));

        var session = CreateSession(document);

        Assert.Throws<InvalidOperationException>(() => session.ConfigureAll());
    }

    [Fact]
    public void UnknownNodeType_ThrowsAtSessionConstruction()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("unknown", "missing/type"));

        Assert.Throws<InvalidOperationException>(() => CreateSession(document));
    }

    [Fact]
    public void TypeMismatchConnection_ThrowsAtSessionConstruction()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("source", "fake/source"));
        document.Nodes.Add(CreateNode("transform", "fake/transform-csv"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));

        Assert.Throws<InvalidOperationException>(() => CreateSession(document));
    }

    [Fact]
    public void EmptyMetanodeContainer_ConstructsAndConfigures()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(new NodeDocument
        {
            NodeId = "meta",
            NodeTypeId = ContainerTypeIds.MetaNode,
            InnerWorkflow = new WorkflowDocument()
        });

        var session = CreateSession(document);

        session.ConfigureAll();

        Assert.Equal(NodeState.Configured, session.GetNode("meta").State);
    }

    [Fact]
    public void MissingOutputSpec_StaysNotConfiguredWithError()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("broken", "fake/broken"));
        var session = CreateSession(document);

        session.ConfigureAll();

        var broken = session.GetNode("broken");
        Assert.Equal(NodeState.NotConfigured, broken.State);
        Assert.Contains("未提供输出 Spec", broken.LastError);
    }

    [Fact]
    public void OutputGating_ReadOnlyVisibleAfterSucceededAndClearedOnInvalidation()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("source", "fake/source"));
        var session = CreateSession(document);
        session.ConfigureAll();

        var source = session.GetNode("source");

        Assert.False(source.TryReadOutputValue("out", out _));

        session.SetNodeState(source.NodeId, NodeState.Succeeded);
        session.PublishOutputValue(source.NodeId, "out", "hello");

        Assert.True(source.TryReadOutputValue("out", out var value));
        Assert.Equal("hello", value);

        session.NotifyNodeChanged(source.NodeId);

        Assert.Equal(NodeState.Configured, source.State);
        Assert.False(source.TryReadOutputValue("out", out _));
    }

    [Fact]
    public void StateEvents_CarryNodePath()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("source", "fake/source"));
        var session = CreateSession(document);

        NodeStateChangedEventArgs? captured = null;
        session.NodeStateChanged += (_, args) => captured = args;

        session.ConfigureAll();

        Assert.NotNull(captured);
        Assert.Equal("/source", captured!.NodePath);
        Assert.Equal(NodeState.NotConfigured, captured.PreviousState);
        Assert.Equal(NodeState.Configured, captured.NewState);
    }

    private static (WorkflowSession Session, NodeRuntime Source, NodeRuntime Transform, NodeRuntime Sink) CreateChainSession()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("source", "fake/source"));
        document.Nodes.Add(CreateNode("transform", "fake/transform"));
        document.Nodes.Add(CreateNode("sink", "fake/sink"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));

        document.Nodes[0].Settings["text"] = "hello";

        var session = CreateSession(document);
        return (session, session.GetNode("source"), session.GetNode("transform"), session.GetNode("sink"));
    }

    private static WorkflowSession CreateSession(WorkflowDocument document)
    {
        return new WorkflowSession(
            document,
            typeId => typeId switch
            {
                "fake/source" => new FakeSourceDefinition(),
                "fake/transform" => new FakeTransformDefinition(),
                "fake/transform-csv" => new FakeTransformDefinition(inputTypeId: "text/csv"),
                "fake/sink" => new FakeSinkDefinition(),
                "fake/broken" => new FakeBrokenDefinition(),
                _ => null!
            });
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

    private sealed class FakeSourceDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", ValueTypeIds.PlainText)];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            var text = Convert.ToString(request.Settings.TryGetValue("text", out var value) ? value : null) ?? string.Empty;
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?>
                {
                    ["out"] = $"spec:{text}"
                }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var text = Convert.ToString(request.Settings.TryGetValue("text", out var value) ? value : null) ?? string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = text }
            });
        }
    }

    private sealed class FakeTransformDefinition : INodeDefinition
    {
        private readonly string _inputTypeId;

        public FakeTransformDefinition(string inputTypeId = "text/plain")
        {
            _inputTypeId = inputTypeId;
        }

        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", _inputTypeId)];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("result", ValueTypeIds.PlainText)];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            var spec = request.InputSpecs.TryGetValue("incoming", out var value) ? value : null;
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?>
                {
                    ["result"] = $"{spec}|x"
                }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var value = request.InputValues.TryGetValue("incoming", out var input) ? input : null;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["result"] = value?.ToString() ?? string.Empty }
            });
        }
    }

    private sealed class FakeSinkDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", ValueTypeIds.PlainText)];

        public IReadOnlyList<NodePortDefinition> OutputPorts => [];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?>()
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?>()
            });
        }
    }

    private sealed class FakeBrokenDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", ValueTypeIds.PlainText)];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?>()
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = null }
            });
        }
    }
}
