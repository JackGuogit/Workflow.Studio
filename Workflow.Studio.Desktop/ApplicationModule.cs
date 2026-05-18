using Autofac;
using Workflow.Studio.Desktop.ViewModels;

namespace Workflow.Studio.Desktop;

public sealed class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MainWindowViewModel>()
            .SingleInstance();

        builder.RegisterType<MainWindow>()
            .AsSelf()
            .SingleInstance();
    }
}
