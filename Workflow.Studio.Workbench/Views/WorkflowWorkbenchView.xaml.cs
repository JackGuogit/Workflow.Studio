using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Workflow.Studio.Workbench.ViewModels;

namespace Workflow.Studio.Workbench.Views;

public partial class WorkflowWorkbenchView : UserControl
{
    public WorkflowWorkbenchView()
    {
        InitializeComponent();
    }

    private void Node_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NodeViewModel nodeViewModel }
            || DataContext is not WorkflowWorkbenchViewModel workbenchViewModel)
        {
            return;
        }

        var editorWindow = new NodeParameterEditorWindow(new NodeParameterEditorViewModel(nodeViewModel))
        {
            Owner = Window.GetWindow(this)
        };

        if (editorWindow.ShowDialog() == true && editorWindow.EditedParameters is not null)
        {
            workbenchViewModel.UpdateNodeParameters(nodeViewModel, editorWindow.EditedParameters);
        }

        e.Handled = true;
    }
}
