using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using Workflow.Studio.Workbench.Editor;

namespace Workflow.Studio.Workbench.Views.Editor;

public partial class WorkbenchView : UserControl
{
    private WorkflowEditorViewModel? _editor;

    public WorkbenchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _editor = DataContext as WorkflowEditorViewModel;
    }

    private void OnNodeMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NodeViewModel node }
            && node.IsMetaNode
            && _editor?.TryNavigateInto(node.NodeId) == true)
        {
            e.Handled = true;
        }
    }

    private void OnEditVariableMappingsClicked(object sender, RoutedEventArgs e)
    {
        if (_editor is null || _editor.SelectedNodeIds.Count != 1)
        {
            return;
        }

        var metaNodeId = _editor.SelectedNodeIds.First();
        var meta = _editor.Document.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, metaNodeId, StringComparison.OrdinalIgnoreCase)
            && node.InnerWorkflow is not null);

        if (meta is null)
        {
            return;
        }

        var window = new VariableMappingEditorWindow(_editor, metaNodeId)
        {
            Owner = Window.GetWindow(this)
        };

        window.ShowDialog();
    }

    private void OnEditNodeSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (_editor is null || _editor.SelectedNodeIds.Count != 1)
        {
            return;
        }

        var nodeId = _editor.SelectedNodeIds.First();
        var node = _editor.Document.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        if (node is null || _editor.GetSettingsSchema(node.NodeTypeId).Count == 0)
        {
            return;
        }

        var window = new NodeSettingsEditorWindow(_editor, node)
        {
            Owner = Window.GetWindow(this)
        };

        window.ShowDialog();
    }

    private void OnLibraryNodeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NodeLibraryItemViewModel item } && e.LeftButton == MouseButtonState.Pressed)
        {
            var data = new DataObject("WorkflowNodeType", item.TypeId);
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
            e.Handled = true;
        }
    }

    private void OnEditorDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("WorkflowNodeType") ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnEditorDrop(object sender, DragEventArgs e)
    {
        if (_editor is null
            || !e.Data.GetDataPresent("WorkflowNodeType")
            || e.Data.GetData("WorkflowNodeType") is not string typeId
            || Editor.ViewportTransform?.Inverse is not { } inverse)
        {
            return;
        }

        var world = inverse.Transform(e.GetPosition(Editor));
        _editor.AddNode(typeId, world.X, world.Y);
        e.Handled = true;
    }
}
