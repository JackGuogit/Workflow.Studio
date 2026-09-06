using Autofac;
using Workflow.Studio.Desktop.Services;
using Workflow.Studio.Workbench.Services;

namespace Workflow.Studio.Desktop;

public sealed class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<WorkflowDocumentPickerService>()
            .As<IWorkflowDocumentPickerService>()
            .SingleInstance();

        builder.RegisterType<Views.WorkspaceHostWindow>()
            .AsSelf()
            .SingleInstance();
    }
}
