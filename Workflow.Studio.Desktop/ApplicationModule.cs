using Autofac;
using Workflow.Studio.Desktop.ViewModels;
using Workflow.Studio.Desktop.Services;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Services;
using Workflow.Studio.Nodes;
using Workflow.Studio.Plugins.BuiltIn;
using Workflow.Studio.Workbench.Services;
using Workflow.Studio.Workbench.ViewModels;

namespace Workflow.Studio.Desktop;

public sealed class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<UppercaseTransformPlugin>()
            .As<IWorkflowPlugin>()
            .As<ITextTransformPlugin>()
            .SingleInstance();

        builder.RegisterType<PreviewPlugin>()
            .As<IWorkflowPlugin>()
            .As<IPreviewPlugin>()
            .SingleInstance();

        builder.RegisterType<OpenCvImageCodecPlugin>()
            .As<IWorkflowPlugin>()
            .As<IOpenCvImageCodecPlugin>()
            .SingleInstance();

        builder.RegisterType<OpenCvImageProcessingPlugin>()
            .As<IWorkflowPlugin>()
            .As<IOpenCvImageProcessingPlugin>()
            .SingleInstance();

        builder.Register(_ =>
            {
                var pluginManager = new PluginManager();

                foreach (var plugin in _.Resolve<IEnumerable<IWorkflowPlugin>>())
                {
                    pluginManager.Register(plugin);
                }

                pluginManager.InitializeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
                return pluginManager;
            })
            .SingleInstance();

        builder.Register(_ =>
            {
                var nodeManager = new NodeManager();
                nodeManager.RegisterType(new TextSourceNode());
                nodeManager.RegisterType(new CsvReadNode());
                nodeManager.RegisterType(new UppercaseTransformNode(_.Resolve<ITextTransformPlugin>()));
                nodeManager.RegisterType(new CsvToTsvTransformNode());
                nodeManager.RegisterType(new PreviewNode(_.Resolve<IPreviewPlugin>()));
                nodeManager.RegisterType(new TsvSaveNode());
                nodeManager.RegisterType(new OpenCvImageReadNode(_.Resolve<IOpenCvImageCodecPlugin>()));
                nodeManager.RegisterType(new OpenCvGrayscaleNode(_.Resolve<IOpenCvImageProcessingPlugin>()));
                nodeManager.RegisterType(new OpenCvThresholdNode(_.Resolve<IOpenCvImageProcessingPlugin>()));
                nodeManager.RegisterType(new OpenCvImageSaveNode(_.Resolve<IOpenCvImageCodecPlugin>()));
                return nodeManager;
            })
            .SingleInstance();

        builder.RegisterType<WorkflowEventHub>()
            .SingleInstance();

        builder.RegisterType<WorkflowEngine>()
            .SingleInstance();

        builder.RegisterType<NodeFactory>()
            .SingleInstance();

        builder.RegisterType<WorkflowConnectionValidator>()
            .As<IWorkflowConnectionValidator>()
            .SingleInstance();

        builder.RegisterType<WorkflowDebugController>()
            .As<IWorkflowDebugController>()
            .SingleInstance();

        builder.RegisterType<WorkflowPersistenceService>()
            .As<IWorkflowPersistenceService>()
            .SingleInstance();

        builder.RegisterType<WorkflowDocumentPickerService>()
            .As<IWorkflowDocumentPickerService>()
            .SingleInstance();

        builder.RegisterType<WorkflowWorkbenchViewModel>()
            .SingleInstance();

        builder.RegisterType<MainWindowViewModel>()
            .SingleInstance();

        builder.RegisterType<MainWindow>()
            .AsSelf()
            .SingleInstance();
    }
}
