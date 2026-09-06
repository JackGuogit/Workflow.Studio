using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class ContainerVariableTests
{
    [Fact]
    public async Task VariableMappings_InAndOut_PropagateAcrossContainerBoundary()
    {
        var sinkDef = new ContainerVariableCaptureSinkDefinition();
        var defs = new Dictionary<string, INodeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["combo"] = new ContainerComboDefinition(),
            ["capture"] = sinkDef
        };

        var document = BuildDocument();
        var session = new WorkflowSession(document, typeId =>
            defs.TryGetValue(typeId, out var definition) ? definition : null!);
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal("outer-value", sinkDef.LastInput);
        Assert.Equal("outer-value", sinkDef.LastVariables["outerVar"]);
        Assert.Equal(NodeState.Succeeded, session.GetNode("meta1").State);
        Assert.Equal("outer-value", session.GetNode("meta1").ProducedFlowVariables!["outerVar"]);
    }

    private static WorkflowDocument BuildDocument()
    {
        var inner = new WorkflowDocument();
        inner.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seed",
            TypeId = "text/plain",
            DefaultValue = string.Empty
        });

        var combo = new NodeDocument
        {
            NodeId = "inner-combo",
            NodeTypeId = "combo"
        };
        combo.SettingsBindings.Add(new SettingsBinding
        {
            Setting = "text",
            Variable = "seed"
        });
        inner.Nodes.Add(combo);
        inner.Nodes.Add(new NodeDocument
        {
            NodeId = "out-1",
            NodeTypeId = ContainerTypeIds.BoundaryOut,
            Ports =
            [
                new PortDocument { PortId = "in", TypeId = "text/plain" }
            ]
        });
        inner.Connections.Add(Connect("inner-combo", "out", "out-1", "in"));

        var meta = new NodeDocument
        {
            NodeId = "meta1",
            NodeTypeId = ContainerTypeIds.MetaNode,
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
                    Source = "innerVar",
                    Target = "outerVar"
                }
            ],
            InnerWorkflow = inner
        };

        var document = new WorkflowDocument();
        document.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seedText",
            TypeId = "text/plain",
            DefaultValue = "outer-value"
        });
        document.Nodes.Add(meta);
        document.Nodes.Add(new NodeDocument
        {
            NodeId = "sink",
            NodeTypeId = "capture"
        });
        document.Connections.Add(Connect("meta1", "out-1", "sink", "incoming"));
        return document;
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

    private sealed class ContainerComboDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
            [new FlowVariableDeclaration("innerVar", "text/plain")];

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
                OutputVariables = new Dictionary<string, object?> { ["innerVar"] = text ?? string.Empty }
            });
        }
    }

    private sealed class ContainerVariableCaptureSinkDefinition : INodeDefinition
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
            LastInput = request.InputValues.TryGetValue("incoming", out var value) ? Convert.ToString(value) : null;
            LastVariables = new Dictionary<string, object?>(request.Variables, StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?>()
            });
        }
    }
}
