using Autofac;
using System.Windows;

namespace Workflow.Studio.Desktop;

public partial class App : Application
{
    private IContainer? _container;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _container = BuildContainer();
        var mainWindow = _container.Resolve<Views.WorkspaceHostWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _container?.Dispose();
        base.OnExit(e);
    }

    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ApplicationModule>();
        return builder.Build();
    }
}
