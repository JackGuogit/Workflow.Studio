using System.Windows;
using System.Windows.Controls;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Workbench.Editor;

namespace Workflow.Studio.Workbench.Views.Editor;

public partial class VariableMappingEditorWindow : Window
{
    private readonly WorkflowEditorViewModel _editor;
    private readonly string _metaNodeId;

    public VariableMappingEditorWindow(WorkflowEditorViewModel editor, string metaNodeId)
    {
        InitializeComponent();
        _editor = editor;
        _metaNodeId = metaNodeId;

        DirectionCombo.ItemsSource = new[] { VariableMappingDirection.In, VariableMappingDirection.Out };
        RefreshList();
    }

    private void RefreshList()
    {
        var meta = _editor.Document.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, _metaNodeId, StringComparison.OrdinalIgnoreCase));

        MappingList.ItemsSource = meta?.VariableMappings
            .Select(mapping => new MappingItem(mapping)) ?? [];
    }

    private void OnAddClicked(object sender, RoutedEventArgs e)
    {
        if (DirectionCombo.SelectedItem is not VariableMappingDirection direction)
        {
            ShowMessage("请选择方向。");
            return;
        }

        if (_editor.TryAddVariableMapping(
                _metaNodeId,
                direction,
                SourceBox.Text.Trim(),
                TargetBox.Text.Trim(),
                out var error))
        {
            MessageText.Text = string.Empty;
            SourceBox.Clear();
            TargetBox.Clear();
            RefreshList();
        }
        else
        {
            ShowMessage(error ?? "添加失败。");
        }
    }

    private void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        if (MappingList.SelectedItem is MappingItem item)
        {
            var meta = _editor.Document.Nodes.First(node => node.NodeId == _metaNodeId);
            var index = meta.VariableMappings.IndexOf(item.Mapping);
            _editor.RemoveVariableMapping(_metaNodeId, index);
            RefreshList();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowMessage(string message)
    {
        MessageText.Text = message;
    }

    private sealed record MappingItem(VariableMapping Mapping)
    {
        public string DisplayText => $"{Mapping.Direction}: {Mapping.Source} -> {Mapping.Target}";
    }
}
