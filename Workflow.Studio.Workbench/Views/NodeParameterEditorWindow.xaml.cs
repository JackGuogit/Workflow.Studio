using System.Windows;
using Workflow.Studio.Workbench.ViewModels;

namespace Workflow.Studio.Workbench.Views;

public partial class NodeParameterEditorWindow : Window
{
    public NodeParameterEditorWindow(NodeParameterEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public IReadOnlyDictionary<string, object?>? EditedParameters { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NodeParameterEditorViewModel viewModel)
        {
            return;
        }

        if (!viewModel.TryCreateParameterSnapshot(out var parameters, out var error))
        {
            MessageBox.Show(
                this,
                error ?? "参数校验失败。",
                "保存参数失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        EditedParameters = parameters;
        DialogResult = true;
    }
}
