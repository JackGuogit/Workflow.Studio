using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowExecutorTests
{
    [Fact]
    public async Task ExecuteAll_Chain_ProducesOutputsFromUpstreamValues()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ExecSourceDefinition();
            defs["transform"] = new ExecTransformDefinition();
            defs["sink"] = new ExecSinkDefinition();
        });

        var document = session.Document;
        document.Nodes.Add(CreateNode("source", "source"));
        document.Nodes.Add(CreateNode("transform", "transform"));
        document.Nodes.Add(CreateNode("sink", "sink"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));
        document.Nodes[0].Settings["text"] = "hello";

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal(3, result.ExecutedNodeIds.Count);
        Assert.Equal(NodeState.Succeeded, session.GetNode("source").State);
        Assert.Equal(NodeState.Succeeded, session.GetNode("transform").State);
        Assert.Equal(NodeState.Succeeded, session.GetNode("sink").State);
        Assert.True(session.GetNode("source").TryReadOutputValue("out", out var sourceValue));
        Assert.Equal("hello", sourceValue);
        Assert.True(session.GetNode("transform").TryReadOutputValue("result", out var transformValue));
        Assert.Equal("hello|T", transformValue);
    }

    [Fact]
    public async Task ExecuteUpTo_CleanReuse_SkipsExecutedUpstreamAndRerunsOnlyDirtyChain()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ExecSourceDefinition();
            defs["transform"] = new ExecTransformDefinition();
            defs["sink"] = new ExecSinkDefinition();
        });
        var document = session.Document;
        document.Nodes.Add(CreateNode("source", "source"));
        document.Nodes.Add(CreateNode("transform", "transform"));
        document.Nodes.Add(CreateNode("sink", "sink"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));
        document.Nodes[0].Settings["text"] = "hello";

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var sourceDef = (ExecSourceDefinition)defs["source"];
        var transformDef = (ExecTransformDefinition)defs["transform"];
        var sinkDef = (ExecSinkDefinition)defs["sink"];

        var all = await executor.ExecuteAllAsync();
        Assert.Equal(3, all.ExecutedNodeIds.Count);
        Assert.Equal(1, sourceDef.ExecuteCount);
        Assert.Equal(1, transformDef.ExecuteCount);
        Assert.Equal(1, sinkDef.ExecuteCount);

        // 目标已执行且 clean：无动作。
        var noop = await executor.ExecuteUpToAsync("sink");
        Assert.Empty(noop.ExecutedNodeIds);

        // transform 被改动 → transform 与 sink 失效；source clean 复用。
        session.NotifyNodeChanged("transform");
        Assert.Equal(NodeState.Succeeded, session.GetNode("source").State);

        var rerun = await executor.ExecuteUpToAsync("sink");

        Assert.Equal(2, rerun.ExecutedNodeIds.Count);
        Assert.Contains("transform", rerun.ExecutedNodeIds);
        Assert.Contains("sink", rerun.ExecutedNodeIds);
        Assert.Equal(1, sourceDef.ExecuteCount);
        Assert.Equal(2, transformDef.ExecuteCount);
        Assert.Equal(2, sinkDef.ExecuteCount);
        Assert.Equal(NodeState.Succeeded, session.GetNode("transform").State);
        Assert.Equal(NodeState.Succeeded, session.GetNode("sink").State);
    }

    [Fact]
    public async Task ExecuteFrom_RerunsSelectedNodeAndDownstream_NotUpstream()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ExecSourceDefinition();
            defs["transform"] = new ExecTransformDefinition();
            defs["sink"] = new ExecSinkDefinition();
        });
        var document = session.Document;
        document.Nodes.Add(CreateNode("source", "source"));
        document.Nodes.Add(CreateNode("transform", "transform"));
        document.Nodes.Add(CreateNode("sink", "sink"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));
        document.Nodes[0].Settings["text"] = "hello";

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var sourceDef = (ExecSourceDefinition)defs["source"];
        var transformDef = (ExecTransformDefinition)defs["transform"];
        var sinkDef = (ExecSinkDefinition)defs["sink"];

        await executor.ExecuteAllAsync();

        var rerun = await executor.ExecuteFromAsync("transform");

        Assert.Equal(2, rerun.ExecutedNodeIds.Count);
        Assert.Contains("transform", rerun.ExecutedNodeIds);
        Assert.Contains("sink", rerun.ExecutedNodeIds);
        Assert.Equal(1, sourceDef.ExecuteCount);
        Assert.Equal(2, transformDef.ExecuteCount);
        Assert.Equal(2, sinkDef.ExecuteCount);
    }

    [Fact]
    public async Task Failure_BlocksDownstreamAndIndependentBranchContinues()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ExecSourceDefinition();
            defs["sourceB"] = new ExecSourceDefinition();
            defs["fail"] = new FailingDefinition();
            defs["sink"] = new ExecSinkDefinition();
        });
        var document = session.Document;
        document.Nodes.Add(CreateNode("sourceA", "source"));
        document.Nodes.Add(CreateNode("fail", "fail"));
        document.Nodes.Add(CreateNode("sinkA", "sink"));
        document.Nodes.Add(CreateNode("sourceB", "sourceB"));
        document.Nodes.Add(CreateNode("sinkB", "sink"));
        document.Connections.Add(Connect("sourceA", "out", "fail", "incoming"));
        document.Connections.Add(Connect("fail", "result", "sinkA", "incoming"));
        document.Connections.Add(Connect("sourceB", "out", "sinkB", "incoming"));

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);

        var result = await executor.ExecuteAllAsync();

        Assert.Contains("fail", result.FailedNodeIds);
        Assert.Contains("sinkA", result.BlockedNodeIds);
        Assert.Equal(NodeState.Succeeded, session.GetNode("sourceA").State);
        Assert.Equal(NodeState.Failed, session.GetNode("fail").State);
        Assert.Equal(NodeState.Blocked, session.GetNode("sinkA").State);
        Assert.Equal(NodeState.Succeeded, session.GetNode("sourceB").State);
        Assert.Equal(NodeState.Succeeded, session.GetNode("sinkB").State);
        Assert.Contains("boom", session.GetNode("fail").LastError);
    }

    [Fact]
    public async Task Concurrency_RespectsConfiguredCap()
    {
        var probe = new ConcurrencyProbe();
        var (session, defs) = CreateSession(defs => { defs["slow"] = new SlowSourceDefinition(probe); });
        var document = session.Document;
        document.Nodes.Add(CreateNode("s1", "slow"));
        document.Nodes.Add(CreateNode("s2", "slow"));
        document.Nodes.Add(CreateNode("s3", "slow"));
        document.Nodes.Add(CreateNode("s4", "slow"));

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal(4, result.ExecutedNodeIds.Count);
        Assert.InRange(probe.MaxConcurrent, 2, 2);
    }

    [Fact]
    public async Task Cancellation_StopsSchedulingFurtherNodes()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["slow"] = new SlowSourceDefinition(new ConcurrencyProbe());
            defs["transform"] = new ExecTransformDefinition();
            defs["sink"] = new ExecSinkDefinition();
        });
        var document = session.Document;
        document.Nodes.Add(CreateNode("source", "slow"));
        document.Nodes.Add(CreateNode("transform", "transform"));
        document.Nodes.Add(CreateNode("sink", "sink"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.NodeStateChanged += (_, args) =>
        {
            if (args.NodeId == "source" && args.NewState == NodeState.Running)
            {
                started.TrySetResult(true);
            }
        };

        using var cancellation = new CancellationTokenSource();
        var runTask = executor.ExecuteAllAsync(cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var result = await runTask;

        Assert.True(result.IsCanceled);
        Assert.Contains("source", result.CanceledNodeIds);
        Assert.Equal(NodeState.Failed, session.GetNode("source").State);
        Assert.NotEqual(NodeState.Succeeded, session.GetNode("transform").State);
        Assert.NotEqual(NodeState.Succeeded, session.GetNode("sink").State);
    }

    [Fact]
    public async Task Breakpoint_PausesNodeAndResumesOnSignal()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["source"] = new ExecSourceDefinition();
            defs["transform"] = new ExecTransformDefinition();
            defs["sink"] = new ExecSinkDefinition();
        });
        var document = session.Document;
        document.Nodes.Add(CreateNode("source", "source"));
        document.Nodes.Add(CreateNode("transform", "transform"));
        document.Nodes.Add(CreateNode("sink", "sink"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));
        document.Nodes[0].IsBreakpointEnabled = true;

        session = Rebuild(session, defs);
        var gateHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var executor = new WorkflowExecutor(
            session,
            maxConcurrency: 1,
            breakpointGate: async (node, cancellationToken) =>
            {
                if (node.NodeId == "source")
                {
                    gateHit.TrySetResult(true);
                    await resume.Task.WaitAsync(cancellationToken);
                }
            });

        var executeTask = executor.ExecuteAllAsync();

        await gateHit.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(NodeState.Paused, session.GetNode("source").State);
        Assert.False(executeTask.IsCompleted);

        resume.TrySetResult(true);
        var result = await executeTask;

        Assert.False(result.HasFailures);
        Assert.Equal(3, result.ExecutedNodeIds.Count);
        Assert.Equal(NodeState.Succeeded, session.GetNode("source").State);
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

    private sealed class ConcurrencyProbe
    {
        private readonly object _sync = new();
        private int _current;
        private int _max;

        public int MaxConcurrent
        {
            get
            {
                lock (_sync)
                {
                    return _max;
                }
            }
        }

        public IDisposable Enter()
        {
            lock (_sync)
            {
                _current++;
                if (_current > _max)
                {
                    _max = _current;
                }
            }

            return new ExitScope(this);
        }

        private sealed class ExitScope : IDisposable
        {
            private readonly ConcurrencyProbe _probe;

            public ExitScope(ConcurrencyProbe probe)
            {
                _probe = probe;
            }

            public void Dispose()
            {
                lock (_probe._sync)
                {
                    _probe._current--;
                }
            }
        }
    }

    private sealed class ExecSourceDefinition : INodeDefinition
    {
        public int ExecuteCount;

        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/plain" }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ExecuteCount);
            var text = request.Settings.TryGetValue("text", out var value) ? Convert.ToString(value) : string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = text ?? string.Empty }
            });
        }
    }

    private sealed class ExecTransformDefinition : INodeDefinition
    {
        public int ExecuteCount;

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
            Interlocked.Increment(ref ExecuteCount);
            var input = request.InputValues.TryGetValue("incoming", out var value) ? Convert.ToString(value) : string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["result"] = $"{input}|T" }
            });
        }
    }

    private sealed class ExecSinkDefinition : INodeDefinition
    {
        public int ExecuteCount;

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
            Interlocked.Increment(ref ExecuteCount);
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?>()
            });
        }
    }

    private sealed class FailingDefinition : INodeDefinition
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
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class SlowSourceDefinition : INodeDefinition
    {
        private readonly ConcurrencyProbe _probe;

        public SlowSourceDefinition(ConcurrencyProbe probe)
        {
            _probe = probe;
        }

        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/plain" }
            };
        }

        public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            using (_probe.Enter())
            {
                await Task.Delay(80, cancellationToken);
            }

            return new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = "slow" }
            };
        }
    }
}
