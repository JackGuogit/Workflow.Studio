using System.Windows;
using Workflow.Studio.Desktop.ViewModels;

namespace Workflow.Studio.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
