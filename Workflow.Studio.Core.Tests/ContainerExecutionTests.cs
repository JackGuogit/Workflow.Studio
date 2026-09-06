using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class ContainerExecutionTests
{
    [Fact]
    public async Task NestedWorkflow_ExecutesBoundaryInOut_AndPropagatesValue()
    {
        var sinkDef = new ContainerCaptureSinkDefinition();
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ContainerSourceDefinition();
            defs["transform"] = new ContainerTransformDefinition();
            defs["sink"] = sinkDef;
        });

        var document = session.Document;
        document.Nodes.Add(CreateNode("root-source", "source"));
        document.Nodes.Add(CreateMetaNode("meta1"));
        document.Nodes.Add(CreateNode("root-sink", "sink"));
        document.Connections.Add(Connect("root-source", "out", "meta1", "in-1"));
        document.Connections.Add(Connect("meta1", "out-1", "root-sink", "incoming"));
        document.Nodes[0].Settings["text"] = "hello";

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures, string.Join(" | ",
            session.Nodes.Select(node => $"{node.NodeId}:{node.State}:{node.LastError}")));
        Assert.Equal(NodeState.Succeeded, session.GetNode("meta1").State);
        Assert.Equal(NodeState.Succeeded, session.GetNode("root-sink").State);
        Assert.Equal("hello|T", sinkDef.LastInput);
        Assert.Equal("/meta1", session.GetNode("meta1").Path);
    }

    [Fact]
    public async Task Configure_PropagatesSpecThroughContainerBoundary()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ContainerSourceDefinition();
            defs["transform"] = new ContainerTransformDefinition();
            defs["sink"] = new ContainerSinkDefinition();
        });

        var document = session.Document;
        document.Nodes.Add(CreateNode("root-source", "source"));
        document.Nodes.Add(CreateMetaNode("meta1"));
        document.Nodes.Add(CreateNode("root-sink", "sink"));
        document.Connections.Add(Connect("root-source", "out", "meta1", "in-1"));
        document.Connections.Add(Connect("meta1", "out-1", "root-sink", "incoming"));
        document.Nodes[0].Settings["text"] = "hello";

        session = Rebuild(session, defs);
        session.ConfigureAll();

        var meta = session.GetNode("meta1");
        var sink = session.GetNode("root-sink");
        Assert.Equal(NodeState.Configured, meta.State);
        Assert.Equal(NodeState.Configured, sink.State);
        Assert.NotNull(meta.FindOutput("out-1"));
        Assert.Equal("spec:hello|x", meta.FindOutput("out-1")!.Spec);
    }

    [Fact]
    public async Task InnerFailure_FailsContainer_AndBlocksOuterDownstream()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ContainerSourceDefinition();
            defs["fail"] = new ContainerFailingDefinition();
            defs["sink"] = new ContainerSinkDefinition();
        });

        var document = session.Document;
        document.Nodes.Add(CreateNode("root-source", "source"));
        document.Nodes.Add(CreateMetaNode("meta1", innerTransformTypeId: "fail"));
        document.Nodes.Add(CreateNode("root-sink", "sink"));
        document.Connections.Add(Connect("root-source", "out", "meta1", "in-1"));
        document.Connections.Add(Connect("meta1", "out-1", "root-sink", "incoming"));

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);

        var result = await executor.ExecuteAllAsync();

        Assert.Contains("meta1", result.FailedNodeIds);
        Assert.Contains("root-sink", result.BlockedNodeIds);
        Assert.Equal(NodeState.Succeeded, session.GetNode("root-source").State);
        Assert.Equal(NodeState.Failed, session.GetNode("meta1").State);
        Assert.Equal(NodeState.Blocked, session.GetNode("root-sink").State);
        Assert.Contains("子工作流执行失败", session.GetNode("meta1").LastError);
    }

    private static NodeDocument CreateMetaNode(string metaNodeId, string innerTransformTypeId = "transform")
    {
        var inner = new WorkflowDocument();
        inner.Nodes.Add(new NodeDocument
        {
            NodeId = "in-1",
            NodeTypeId = ContainerTypeIds.BoundaryIn,
            Ports =
            [
                new PortDocument { PortId = "out", TypeId = "text/plain" }
            ]
        });
        inner.Nodes.Add(new NodeDocument
        {
            NodeId = "inner-transform",
            NodeTypeId = innerTransformTypeId
        });
        inner.Nodes.Add(new NodeDocument
        {
            NodeId = "out-1",
            NodeTypeId = ContainerTypeIds.BoundaryOut,
            Ports =
            [
                new PortDocument { PortId = "in", TypeId = "text/plain" }
            ]
        });
        inner.Connections.Add(Connect("in-1", "out", "inner-transform", "incoming"));
        inner.Connections.Add(Connect("inner-transform", "result", "out-1", "in"));

        return new NodeDocument
        {
            NodeId = metaNodeId,
            NodeTypeId = ContainerTypeIds.MetaNode,
            InnerWorkflow = inner
        };
    }

    private static (WorkflowSession Session, Dictionary<string, INodeDefinition> Defs) CreateSession(
        Action<Dictionary<string, INodeDefinition>> populate)
    {
        var defs = new Dictionary<string, INodeDefinition>(StringComparer.OrdinalIgnoreCase);
        populate(defs);
        var session = new WorkflowSession(new WorkflowDocument(), typeId =>
            defs.TryGetValue(typeId, out var definition) ? definition : null!);
        return (session, defs);
    }

    private static WorkflowSession Rebuild(WorkflowSession session, Dictionary<string, INodeDefinition> defs)
    {
        return new WorkflowSession(session.Document, typeId =>
            defs.TryGetValue(typeId, out var definition) ? definition : null!);
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

    private sealed class ContainerSourceDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            var text = request.Settings.TryGetValue("text", out var value) ? Convert.ToString(value) : string.Empty;
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["out"] = $"spec:{text}" }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var text = request.Settings.TryGetValue("text", out var value) ? Convert.ToString(value) : string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = text ?? string.Empty }
            });
        }
    }

    private sealed class ContainerTransformDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("result", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            var spec = request.InputSpecs.TryGetValue("incoming", out var value) ? value : null;
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["result"] = $"{spec}|x" }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var input = request.InputValues.TryGetValue("incoming", out var value) ? Convert.ToString(value) : string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["result"] = $"{input}|T" }
            });
        }
    }

    private sealed class ContainerFailingDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("result", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["result"] = "text/plain" }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("inner boom");
        }
    }

    private sealed class ContainerSinkDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

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

    private sealed class ContainerCaptureSinkDefinition : INodeDefinition
    {
        public string? LastInput { get; private set; }

        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

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
            LastInput = request.InputValues.TryGetValue("incoming", out var value) ? Convert.ToString(value) : null;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?>()
            });
        }
    }
}
