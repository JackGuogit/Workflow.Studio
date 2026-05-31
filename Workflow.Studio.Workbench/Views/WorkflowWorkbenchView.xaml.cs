using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Workflow.Studio.Nodify;
using Workflow.Studio.Workbench.ViewModels;

namespace Workflow.Studio.Workbench.Views;

public partial class WorkflowWorkbenchView : UserControl
{
    private Point _libraryDragStartPoint;
    private bool _isLibraryDragPending;

    static WorkflowWorkbenchView()
    {
        WorkflowWorkbenchConnectorInteractions.Register();
    }

    public WorkflowWorkbenchView()
    {
        InitializeComponent();
    }

    private void LibraryNode_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _libraryDragStartPoint = e.GetPosition(this);
        _isLibraryDragPending = sender is FrameworkElement { DataContext: NodeLibraryItemViewModel };
    }

    private void LibraryNode_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isLibraryDragPending = false;
    }

    private void LibraryNode_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isLibraryDragPending || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        var dragOffset = currentPosition - _libraryDragStartPoint;

        if (Math.Abs(dragOffset.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(dragOffset.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is not FrameworkElement { DataContext: NodeLibraryItemViewModel item } element)
        {
            return;
        }

        _isLibraryDragPending = false;

        var data = new DataObject(typeof(NodeLibraryItemViewModel), item);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void Editor_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(NodeLibraryItemViewModel)))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Editor_Drop(object sender, DragEventArgs e)
    {
        if (sender is not NodifyEditor editor
            || DataContext is not WorkflowWorkbenchViewModel workbenchViewModel
            || e.Data.GetData(typeof(NodeLibraryItemViewModel)) is not NodeLibraryItemViewModel item)
        {
            return;
        }

        var location = editor.GetLocationInsideEditor(e);
        var snappedLocation = new Point(
            editor.SnapToGrid(location.X),
            editor.SnapToGrid(location.Y));

        workbenchViewModel.AddNode(item, snappedLocation);
        e.Handled = true;
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

        if (editorWindow.ShowDialog() == true)
        {
            workbenchViewModel.NotifyNodeSettingsChanged(nodeViewModel);
        }

        e.Handled = true;
    }
}
