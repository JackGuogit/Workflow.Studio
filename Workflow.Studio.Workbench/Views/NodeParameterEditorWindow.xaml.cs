using System.Windows;
using Workflow.Studio.Workbench.ViewModels;

namespace Workflow.Studio.Workbench.Views;

public partial class NodeParameterEditorWindow : Window
{
    public NodeParameterEditorWindow(NodeParameterEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (viewModel.CreateSettingsView(out var error) is { } editorView)
        {
            EditorHost.Content = editorView;
        }
        else if (!string.IsNullOrWhiteSpace(error))
        {
            MessageBox.Show(
                this,
                error,
                "创建设置视图失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NodeParameterEditorViewModel viewModel)
        {
            return;
        }

        if (!viewModel.TryApplyChanges(out var error))
        {
            MessageBox.Show(
                this,
                error ?? "设置校验失败。",
                "保存设置失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
