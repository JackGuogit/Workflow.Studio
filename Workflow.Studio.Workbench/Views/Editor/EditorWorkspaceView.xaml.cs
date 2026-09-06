using System.Windows.Controls;
using Workflow.Studio.Workbench.Editor;

namespace Workflow.Studio.Workbench.Views.Editor;

public partial class EditorWorkspaceView : UserControl
{
    public EditorWorkspaceView()
    {
        InitializeComponent();
    }

    private async void OnRecentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentCombo.SelectedItem is RecentFileItem item
            && DataContext is EditorWorkspaceViewModel workspace)
        {
            await workspace.OpenRecentFileAsync(item.Path);
            RecentCombo.SelectedItem = null;
        }
    }
}
