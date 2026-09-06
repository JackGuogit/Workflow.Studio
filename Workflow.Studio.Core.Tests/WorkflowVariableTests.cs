using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowVariableTests
{
    [Fact]
    public async Task SettingsBinding_ResolvesEntryVariable_AndUpdatePropagates()
    {
        var sourceDef = new VariableSourceDefinition("lastText");
        var sinkDef = new CaptureSinkDefinition();
        var (session, defs) = CreateSession(defs =>
        {
            defs["var-source"] = sourceDef;
            defs["capture"] = sinkDef;
        });

        var document = session.Document;
        document.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seedText",
            TypeId = "text/plain",
            DefaultValue = "DevWorkflow Studio"
        });
        document.Nodes.Add(CreateNode("source", "var-source"));
        document.Nodes[0].SettingsBindings.Add(new SettingsBinding
        {
            Setting = "text",
            Variable = "seedText"
        });
        document.Nodes.Add(CreateNode("sink", "capture"));
        document.Connections.Add(Connect("source", "out", "sink", "incoming"));

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal("DevWorkflow Studio", sinkDef.LastInput);
        Assert.Equal("DevWorkflow Studio", sinkDef.LastVariables["lastText"]);

        session.SetEntryVariable("seedText", "updated");
        await executor.ExecuteFromAsync("source");

        Assert.Equal("updated", sinkDef.LastInput);
        Assert.Equal("updated", sinkDef.LastVariables["lastText"]);
    }

    [Fact]
    public async Task FlowVariables_VisibleToTransitiveDownstream()
    {
        var sourceDef = new VariableSourceDefinition("varA");
        var transformDef = new VariableTransformDefinition("varA");
        var sinkDef = new CaptureSinkDefinition();
        var (session, defs) = CreateSession(defs =>
        {
            defs["var-source"] = sourceDef;
            defs["var-transform"] = transformDef;
            defs["capture"] = sinkDef;
        });

        var document = session.Document;
        document.Nodes.Add(CreateNode("source", "var-source"));
        document.Nodes.Add(CreateNode("transform", "var-transform"));
        document.Nodes.Add(CreateNode("sink", "capture"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));
        document.Nodes[0].Settings["text"] = "A";

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal("A|A", transformDef.LastOutput);
        Assert.Equal("A", sinkDef.LastVariables["varA"]);
        Assert.Equal("A|A", sinkDef.LastVariables["lastResult"]);
    }

    [Fact]
    public async Task SameVariableNameInDisjointBranches_StaysIsolated()
    {
        var sinkADef = new CaptureSinkDefinition();
        var sinkBDef = new CaptureSinkDefinition();
        var (session, defs) = CreateSession(defs =>
        {
            defs["var-source-a"] = new VariableSourceDefinition("dup");
            defs["var-source-b"] = new VariableSourceDefinition("dup");
            defs["capture-a"] = sinkADef;
            defs["capture-b"] = sinkBDef;
        });

        var document = session.Document;
        document.Nodes.Add(CreateNode("sourceA", "var-source-a"));
        document.Nodes.Add(CreateNode("sinkA", "capture-a"));
        document.Nodes.Add(CreateNode("sourceB", "var-source-b"));
        document.Nodes.Add(CreateNode("sinkB", "capture-b"));
        document.Connections.Add(Connect("sourceA", "out", "sinkA", "incoming"));
        document.Connections.Add(Connect("sourceB", "out", "sinkB", "incoming"));
        document.Nodes[0].Settings["text"] = "A";
        document.Nodes[2].Settings["text"] = "B";

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal("A", sinkADef.LastVariables["dup"]);
        Assert.Equal("B", sinkBDef.LastVariables["dup"]);
        Assert.DoesNotContain("dup2", sinkADef.LastVariables.Keys);
    }

    [Fact]
    public void SameVariableNameMergingAtCommonDownstream_FailsConfigure()
    {
        var (session, defs) = CreateSession(defs =>
        {
            defs["var-source-a"] = new VariableSourceDefinition("dup");
            defs["var-source-b"] = new VariableSourceDefinition("dup");
            defs["join"] = new VariableJoinDefinition();
        });

        var document = session.Document;
        document.Nodes.Add(CreateNode("sourceA", "var-source-a"));
        document.Nodes.Add(CreateNode("sourceB", "var-source-b"));
        document.Nodes.Add(CreateNode("join", "join"));
        document.Connections.Add(Connect("sourceA", "out", "join", "a"));
        document.Connections.Add(Connect("sourceB", "out", "join", "b"));

        session = Rebuild(session, defs);
        session.ConfigureAll();

        var join = session.GetNode("join");
        Assert.Equal(NodeState.NotConfigured, join.State);
        Assert.Contains("同名流变量", join.LastError);
    }

    [Fact]
    public async Task FlowVariableTypeMismatch_FailsNodeExecution()
    {
        var (session, defs) = CreateSession(defs => { defs["bad"] = new BadVariableDefinition(); });
        var document = session.Document;
        document.Nodes.Add(CreateNode("bad", "bad"));

        session = Rebuild(session, defs);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var result = await executor.ExecuteAllAsync();

        Assert.Contains("bad", result.FailedNodeIds);
        Assert.Contains("期望类型", session.GetNode("bad").LastError);
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

    private sealed class VariableSourceDefinition : INodeDefinition
    {
        private readonly string _variableName;

        public VariableSourceDefinition(string variableName)
        {
            _variableName = variableName;
        }

        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
            [new FlowVariableDeclaration(_variableName, "text/plain")];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/plain" }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var text = request.Settings.TryGetValue("text", out var value) ? Convert.ToString(value) : string.Empty;
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = text ?? string.Empty },
                OutputVariables = new Dictionary<string, object?> { [_variableName] = text ?? string.Empty }
            });
        }
    }

    private sealed class VariableTransformDefinition : INodeDefinition
    {
        private readonly string _upstreamVariableName;

        public VariableTransformDefinition(string upstreamVariableName)
        {
            _upstreamVariableName = upstreamVariableName;
        }

        public string? LastOutput { get; private set; }

        public IReadOnlyList<NodePortDefinition> InputPorts =>
            [new NodePortDefinition("incoming", "text/plain")];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("result", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
            [new FlowVariableDeclaration("lastResult", "text/plain")];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["result"] = "text/plain" }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var input = request.InputValues.TryGetValue("incoming", out var inputValue)
                ? Convert.ToString(inputValue)
                : string.Empty;
            var upstreamVariable = request.Variables.TryGetValue(_upstreamVariableName, out var variableValue)
                ? Convert.ToString(variableValue)
                : string.Empty;

            LastOutput = $"{input}|{upstreamVariable}";

            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["result"] = LastOutput },
                OutputVariables = new Dictionary<string, object?> { ["lastResult"] = LastOutput }
            });
        }
    }

    private sealed class CaptureSinkDefinition : INodeDefinition
    {
        public string? LastInput { get; private set; }

        public IReadOnlyDictionary<string, object?> LastVariables { get; private set; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

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
            LastInput = request.InputValues.TryGetValue("incoming", out var value)
                ? Convert.ToString(value)
                : null;
            LastVariables = new Dictionary<string, object?>(request.Variables, StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?>()
            });
        }
    }

    private sealed class VariableJoinDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts =>
        [
            new NodePortDefinition("a", "text/plain"),
            new NodePortDefinition("b", "text/plain")
        ];

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
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = "join" }
            });
        }
    }

    private sealed class BadVariableDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
            [new FlowVariableDeclaration("count", "scalar/int64")];

        public NodeConfigureResult Configure(NodeConfigureRequest request)
        {
            return new NodeConfigureResult
            {
                OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/plain" }
            };
        }

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = "x" },
                OutputVariables = new Dictionary<string, object?> { ["count"] = "not-a-long" }
            });
        }
    }
}
