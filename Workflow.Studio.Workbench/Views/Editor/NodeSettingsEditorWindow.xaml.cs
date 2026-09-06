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
            var current = node.Settings.TryGetValue(field.Key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
            FrameworkElement input = field.EditorKind switch
            {
                "bool" => new CheckBox
                {
                    IsChecked = bool.TryParse(current, out var flag) && flag,
                    VerticalAlignment = VerticalAlignment.Center
                },
                "enum" when field.Options is { Count: > 0 } => new ComboBox
                {
                    ItemsSource = field.Options,
                    SelectedItem = field.Options.FirstOrDefault(option =>
                        string.Equals(option, current, StringComparison.OrdinalIgnoreCase)) ?? field.Options[0],
                    Width = 140,
                    VerticalAlignment = VerticalAlignment.Center
                },
                _ => new TextBox
                {
                    Text = current,
                    Margin = new Thickness(0, 2, 0, 6),
                    Width = 220
                }
            };

            var row = new SettingRow(field, input);
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
                    input
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
            var text = row.Input switch
            {
                CheckBox checkBox => checkBox.IsChecked == true ? "true" : "false",
                ComboBox comboBox => comboBox.SelectedItem as string ?? string.Empty,
                TextBox textBox => textBox.Text,
                _ => string.Empty
            };

            if (row.Field.EditorKind == "number")
            {
                if (!double.TryParse(text, out _))
                {
                    System.Windows.MessageBox.Show(this, $"字段 '{row.Field.DisplayName}' 必须是数字。", "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(text) || _node.Settings.ContainsKey(row.Field.Key))
            {
                _node.Settings[row.Field.Key] = text;
            }
        }

        _editor.NotifyNodeSettingsSaved(_node.NodeId);
        Close();
    }

    private sealed record SettingRow(NodeSettingField Field, FrameworkElement Input);
}
