using System.Windows;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Desktop.Services;
using Workflow.Studio.Workbench.Editor;

namespace Workflow.Studio.Desktop.Views;

public partial class WorkspaceHostWindow : Window
{
    public WorkspaceHostWindow()
    {
        InitializeComponent();

        var workspace = new EditorWorkspaceViewModel(
            EditorHostFactory.BuildRegistry(),
            new WorkflowDocumentPickerService());
        workspace.AddDocument(EditorHostFactory.CreateDemoDocument(), "演示工作流", maxConcurrency: 4);
        workspace.AddDocument(new WorkflowDocument(), "未命名工作流", maxConcurrency: 4);

        DataContext = workspace;
    }
}
