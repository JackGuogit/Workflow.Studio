using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Workbench.Editor;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowEditorViewModelTests
{
    [Fact]
    public async Task AddConnectAndExecute_ProjectSessionStateToViewModels()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowEditorViewModel(document, registry, maxConcurrency: 1);

        var source = editor.AddNode("src", 0, 0);
        source.Model.Settings["text"] = "hello";
        var transform = editor.AddNode("tr", 100, 0);
        var sink = editor.AddNode("cap", 200, 0);

        Assert.True(editor.TryConnect(source.Outputs[0], transform.Inputs[0], out _));
        Assert.True(editor.TryConnect(transform.Outputs[0], sink.Inputs[0], out _));

        var result = await editor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.Equal("Succeeded", editor.Nodes.First(node => node.NodeId == sink.NodeId).StateText);
        Assert.Equal("Succeeded", editor.Nodes.First(node => node.NodeId == transform.NodeId).StateText);
        Assert.Equal(3, editor.Nodes.Count);
        Assert.Equal(2, editor.Connections.Count);
        Assert.Contains(editor.ExecutionLogs, entry => entry.Contains("执行完成"));
    }

    [Fact]
    public async Task EntryVariable_BindingAndUpdateFlowThroughEditor()
    {
        var (document, registry) = CreateWorld();
        document.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seedText",
            TypeId = "text/plain",
            DefaultValue = "A"
        });

        var editor = new WorkflowEditorViewModel(document, registry, maxConcurrency: 1);

        var source = editor.AddNode("src", 0, 0);
        source.Model.SettingsBindings.Add(new SettingsBinding
        {
            Setting = "text",
            Variable = "seedText"
        });
        var sink = editor.AddNode("cap", 100, 0);
        Assert.True(editor.TryConnect(source.Outputs[0], sink.Inputs[0], out _));

        var result = await editor.ExecuteAllAsync();
        Assert.False(result.HasFailures);
        Assert.Equal(NodeState.Succeeded, editor.Session.GetNode(sink.NodeId).State);
        Assert.Equal("A", ((VmCaptureDefinition)registry.TryResolve("cap")!).LastInput);

        editor.SetEntryVariable("seedText", "B");
        await editor.ExecuteAllAsync();

        Assert.Equal("B", ((VmCaptureDefinition)registry.TryResolve("cap")!).LastInput);
        Assert.Single(editor.EntryVariables);
        Assert.Equal("B", editor.EntryVariables[0].Value);
    }

    [Fact]
    public void RemoveNodeAndConnection_KeepCollectionsConsistent()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowEditorViewModel(document, registry);

        var source = editor.AddNode("src", 0, 0);
        var sink = editor.AddNode("cap", 100, 0);
        Assert.True(editor.TryConnect(source.Outputs[0], sink.Inputs[0], out _));

        Assert.True(editor.RemoveConnection(editor.Connections[0]));
        Assert.Empty(editor.Connections);

        Assert.True(editor.RemoveNode(sink.NodeId));
        Assert.Single(editor.Nodes);
        Assert.Empty(editor.Connections);
    }

    [Fact]
    public void NavigateIntoMetanode_AndBack_PreservesBoundaryAndInnerEdits()
    {
        var (document, registry) = CreateWorld();

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
            NodeId = "inner-a",
            NodeTypeId = "tr"
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
        inner.Connections.Add(Connect("in-1", "out", "inner-a", "incoming"));
        inner.Connections.Add(Connect("inner-a", "result", "out-1", "in"));

        document.Nodes.Add(new NodeDocument
        {
            NodeId = "meta1",
            NodeTypeId = ContainerTypeIds.MetaNode,
            InnerWorkflow = inner
        });

        var editor = new WorkflowEditorViewModel(document, registry);

        Assert.True(editor.TryNavigateInto("meta1"));
        Assert.True(editor.CanNavigateBack);
        Assert.Single(editor.Document.Nodes);
        Assert.Equal("inner-a", editor.Document.Nodes[0].NodeId);

        editor.AddNode("tr", 0, 0);
        editor.NavigateBack();

        Assert.False(editor.CanNavigateBack);

        var restored = document.Nodes.Single(node => node.NodeId == "meta1").InnerWorkflow!;
        Assert.Equal(4, restored.Nodes.Count);
        Assert.Equal(2, restored.Nodes.Count(node =>
            node.NodeTypeId is ContainerTypeIds.BoundaryIn or ContainerTypeIds.BoundaryOut));
        Assert.Equal(2, restored.Connections.Count);
        Assert.Equal(2, restored.Connections.Count(connection =>
            connection.SourceNodeId == "in-1" || connection.TargetNodeId == "out-1"));
    }

    [Fact]
    public async Task SaveInsideMetanode_IsRejected()
    {
        var (document, registry) = CreateWorld();
        document.Nodes.Add(new NodeDocument
        {
            NodeId = "meta1",
            NodeTypeId = ContainerTypeIds.MetaNode,
            InnerWorkflow = new WorkflowDocument()
        });

        var editor = new WorkflowEditorViewModel(document, registry);
        Assert.True(editor.TryNavigateInto("meta1"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            editor.SaveAsync(Path.Combine(Path.GetTempPath(), "should-not-save.workflow.json")));
    }

    [Fact]
    public void PackSelectedCommand_PacksSelectedNodesIntoMetanode()
    {
        var (document, registry) = CreateWorld();
        document.Nodes.Add(CreateNode("source", "src"));
        document.Nodes.Add(CreateNode("transform", "tr"));
        document.Nodes.Add(CreateNode("sink", "cap"));
        document.Connections.Add(Connect("source", "out", "transform", "incoming"));
        document.Connections.Add(Connect("transform", "result", "sink", "incoming"));

        var editor = new WorkflowEditorViewModel(document, registry);
        editor.Nodes.First(node => node.NodeId == "source").IsSelected = true;
        editor.Nodes.First(node => node.NodeId == "transform").IsSelected = true;

        Assert.True(editor.PackSelectedCommand.CanExecute(null));
        editor.PackSelectedCommand.Execute(null);

        Assert.Equal(2, editor.Document.Nodes.Count);
        var meta = editor.Document.Nodes.Single(node => node.InnerWorkflow is not null);
        Assert.Equal(3, meta.InnerWorkflow!.Nodes.Count);
        Assert.Empty(editor.SelectedNodeIds);
    }

    [Fact]
    public void VariableMapping_AddAndRemoveWithValidation()
    {
        var registry = new WorkflowDefinitionRegistry();
        registry.Register("prod", new ProducerDefinition(), new NodeTypeDescriptor("prod", "产出者"));

        var document = new WorkflowDocument();
        var inner = new WorkflowDocument();
        inner.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seed",
            TypeId = "text/plain",
            DefaultValue = string.Empty
        });
        inner.Nodes.Add(CreateNode("producer", "prod"));
        document.Nodes.Add(new NodeDocument
        {
            NodeId = "meta1",
            NodeTypeId = ContainerTypeIds.MetaNode,
            InnerWorkflow = inner
        });

        var editor = new WorkflowEditorViewModel(document, registry);

        Assert.True(editor.TryAddVariableMapping("meta1", VariableMappingDirection.In, "seedText", "seed", out _));
        Assert.True(editor.TryAddVariableMapping("meta1", VariableMappingDirection.Out, "innerVar", "outerVar", out _));
        Assert.Equal(2, document.Nodes.Single(node => node.NodeId == "meta1").VariableMappings.Count);

        Assert.False(editor.TryAddVariableMapping("meta1", VariableMappingDirection.In, "x", "missing", out var targetError));
        Assert.Contains("未在子工作流中声明", targetError);
        Assert.False(editor.TryAddVariableMapping("meta1", VariableMappingDirection.Out, "absent", "outerVar", out var sourceError));
        Assert.Contains("未在子工作流中找到产出节点", sourceError);

        Assert.True(editor.RemoveVariableMapping("meta1", 1));
        Assert.Single(document.Nodes.Single(node => node.NodeId == "meta1").VariableMappings);

        var metaViewModel = editor.Nodes.First(node => node.NodeId == "meta1");
        metaViewModel.IsSelected = true;
        Assert.Single(editor.SelectedMetaMappings);

        Assert.True(editor.RemoveVariableMapping("meta1", 0));
        Assert.Empty(editor.SelectedMetaMappings);
    }

    [Fact]
    public void UndoRedo_RestoresStructuralChanges()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowEditorViewModel(document, registry);

        editor.AddNode("src", 0, 0);
        editor.AddNode("tr", 100, 0);
        Assert.Equal(2, editor.Document.Nodes.Count);

        Assert.True(editor.UndoCommand.CanExecute(null));
        editor.UndoCommand.Execute(null);
        Assert.Single(editor.Document.Nodes);

        editor.UndoCommand.Execute(null);
        Assert.Empty(editor.Document.Nodes);

        editor.RedoCommand.Execute(null);
        editor.RedoCommand.Execute(null);
        Assert.Equal(2, editor.Document.Nodes.Count);
        Assert.False(editor.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleBreakpoint_FlipsSelectedNodeFlag()
    {
        var (document, registry) = CreateWorld();
        var editor = new WorkflowEditorViewModel(document, registry);

        var source = editor.AddNode("src", 0, 0);
        source.IsSelected = true;

        Assert.True(editor.ToggleBreakpointCommand.CanExecute(null));
        editor.ToggleBreakpointCommand.Execute(null);

        var refreshed = editor.Nodes.First(node => node.NodeId == source.NodeId);
        Assert.True(refreshed.IsBreakpointEnabled);
        Assert.Contains("断点", refreshed.BreakpointText);

        editor.ToggleBreakpointCommand.Execute(null);
        Assert.False(editor.Nodes.First(node => node.NodeId == source.NodeId).IsBreakpointEnabled);
    }

    private static (WorkflowDocument Document, WorkflowDefinitionRegistry Registry) CreateWorld()
    {
        var registry = new WorkflowDefinitionRegistry();
        registry.Register("src", new VmSourceDefinition(), new NodeTypeDescriptor("src", "源", "Source"));
        registry.Register("tr", new VmTransformDefinition(), new NodeTypeDescriptor("tr", "变换", "Transform"));
        registry.Register("cap", new VmCaptureDefinition(), new NodeTypeDescriptor("cap", "汇点", "Output"));
        return (new WorkflowDocument(), registry);
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

    private static NodeDocument CreateNode(string nodeId, string nodeTypeId)
    {
        return new NodeDocument
        {
            NodeId = nodeId,
            NodeTypeId = nodeTypeId
        };
    }

    private sealed class VmSourceDefinition : INodeDefinition
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

    private sealed class VmTransformDefinition : INodeDefinition
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

    private sealed class VmCaptureDefinition : INodeDefinition
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

    private sealed class ProducerDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

        public IReadOnlyList<NodePortDefinition> OutputPorts =>
            [new NodePortDefinition("out", "text/plain")];

        public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
            [new FlowVariableDeclaration("innerVar", "text/plain")];

        public NodeConfigureResult Configure(NodeConfigureRequest request) =>
            new() { OutputSpecs = new Dictionary<string, object?> { ["out"] = "text/plain" } };

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NodeExecutionResult
            {
                OutputValues = new Dictionary<string, object?> { ["out"] = "x" },
                OutputVariables = new Dictionary<string, object?> { ["innerVar"] = "x" }
            });
    }
}
