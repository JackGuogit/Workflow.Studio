using CommunityToolkit.Mvvm.ComponentModel;

namespace Workflow.Studio.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public string Title { get; } = "Workflow Studio";

    public string WelcomeMessage { get; } = "Autofac 已接入应用启动流程";
}
