using System.IO;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Workbench.Editor;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class EditorWorkspaceViewModelTests
{
    [Fact]
    public void AddAndCloseDocuments_KeepActiveDocumentConsistent()
    {
        var registry = new WorkflowDefinitionRegistry();
        var workspace = new EditorWorkspaceViewModel(registry);

        var first = workspace.AddDocument(new WorkflowDocument(), "文档 A");
        var second = workspace.AddDocument(new WorkflowDocument(), "文档 B");

        Assert.Equal(2, workspace.Documents.Count);
        Assert.Same(second, workspace.ActiveDocument);
        Assert.Equal("文档 B", second.Title);

        workspace.ActiveDocument = first;
        Assert.True(workspace.CloseDocument(first));

        Assert.Single(workspace.Documents);
        Assert.Same(second, workspace.ActiveDocument);
        Assert.True(workspace.HasActiveDocument);
    }

    [Fact]
    public void CloseLastDocument_LeavesNoActiveDocument()
    {
        var workspace = new EditorWorkspaceViewModel(new WorkflowDefinitionRegistry());
        var editor = workspace.AddDocument(new WorkflowDocument(), "唯一文档");

        Assert.True(workspace.CloseDocument(editor));

        Assert.Empty(workspace.Documents);
        Assert.Null(workspace.ActiveDocument);
        Assert.False(workspace.HasActiveDocument);
    }

    [Fact]
    public void NewDocumentCommand_AddsAndActivatesBlankDocument()
    {
        var workspace = new EditorWorkspaceViewModel(new WorkflowDefinitionRegistry());

        Assert.True(workspace.NewDocumentCommand.CanExecute(null));
        workspace.NewDocumentCommand.Execute(null);
        workspace.NewDocumentCommand.Execute(null);

        Assert.Equal(2, workspace.Documents.Count);
        Assert.Same(workspace.Documents[1], workspace.ActiveDocument);
        Assert.Equal("未命名工作流 2", workspace.ActiveDocument!.Title);
    }

    [Fact]
    public void CloseActiveDocumentCommand_ClosesActiveTab()
    {
        var workspace = new EditorWorkspaceViewModel(new WorkflowDefinitionRegistry());
        var first = workspace.AddDocument(new WorkflowDocument(), "A");
        workspace.AddDocument(new WorkflowDocument(), "B");

        Assert.True(workspace.CloseActiveDocumentCommand.CanExecute(null));
        workspace.CloseActiveDocumentCommand.Execute(null);

        Assert.Single(workspace.Documents);
        Assert.Same(first, workspace.ActiveDocument);
    }

    [Fact]
    public async Task SaveAndOpenDocument_RoundTripsThroughFile()
    {
        var registry = new WorkflowDefinitionRegistry();
        registry.Register("demo.node.text-source", new MinimalDefinition(), new NodeTypeDescriptor("demo.node.text-source", "输入节点"));
        var workspace = new EditorWorkspaceViewModel(registry);
        var document = new WorkflowDocument();
        document.Nodes.Add(new NodeDocument
        {
            NodeId = "node-source",
            NodeTypeId = "demo.node.text-source"
        });
        var editor = workspace.AddDocument(document, "待保存");
        var tempPath = Path.Combine(Path.GetTempPath(), $"ws-save-{Guid.NewGuid():N}.workflow.json");

        try
        {
            Assert.True(await workspace.SaveActiveDocumentAsync(tempPath));
            Assert.Equal(Path.GetFileName(tempPath), editor.Title);

            var opened = await workspace.OpenDocumentAsync(tempPath);

            Assert.Same(opened, workspace.ActiveDocument);
            Assert.Single(opened.Document.Nodes);
            Assert.Equal("node-source", opened.Document.Nodes[0].NodeId);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed class MinimalDefinition : INodeDefinition
    {
        public IReadOnlyList<NodePortDefinition> InputPorts => [];

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
}
