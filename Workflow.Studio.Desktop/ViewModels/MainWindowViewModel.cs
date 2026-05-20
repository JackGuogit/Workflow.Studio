using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Workbench.ViewModels;

namespace Workflow.Studio.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(WorkflowWorkbenchViewModel workbench)
    {
        Workbench = workbench;
    }

    public string Title { get; } = "Workflow Studio";

    public WorkflowWorkbenchViewModel Workbench { get; }
}
