using System.Windows;
using System.Windows.Controls;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Workbench.Editor;

namespace Workflow.Studio.Workbench.Views.Editor;

public partial class NodeSettingsEditorWindow : Window
{
    private readonly WorkflowEditorViewModel _editor;
    private readonly NodeDocument _node;
    private readonly List<SettingRow> _rows = [];

    public NodeSettingsEditorWindow(WorkflowEditorViewModel editor, NodeDocument node)
    {
        InitializeComponent();
        _editor = editor;
        _node = node;

        foreach (var field in editor.GetSettingsSchema(node.NodeTypeId))
        {
            var textBox = new TextBox
            {
                Text = node.Settings.TryGetValue(field.Key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty,
                Margin = new Thickness(0, 2, 0, 6),
                Width = 220
            };

            var row = new SettingRow(field, textBox);
            _rows.Add(row);

            FieldList.Items.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = field.DisplayName,
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 110
                    },
                    textBox
                }
            });
        }

        if (_rows.Count == 0)
        {
            FieldList.Items.Add(new TextBlock { Text = "该节点没有可编辑设置。", Foreground = System.Windows.Media.Brushes.Gray });
        }
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            _node.Settings[row.Field.Key] = row.TextBox.Text;
        }

        _editor.NotifyNodeSettingsSaved(_node.NodeId);
        Close();
    }

    private sealed record SettingRow(NodeSettingField Field, TextBox TextBox);
}
