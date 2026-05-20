using Autofac;
using Workflow.Studio.Desktop.ViewModels;
using Workflow.Studio.Core.Nodes.BuiltIn;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Plugins.BuiltIn;
using Workflow.Studio.Core.Services;
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
                nodeManager.RegisterType(new UppercaseTransformNode(_.Resolve<ITextTransformPlugin>()));
                nodeManager.RegisterType(new PreviewNode(_.Resolve<IPreviewPlugin>()));
                return nodeManager;
            })
            .SingleInstance();

        builder.RegisterType<WorkflowEventHub>()
            .SingleInstance();

        builder.RegisterType<WorkflowEngine>()
            .SingleInstance();

        builder.RegisterType<NodeFactory>()
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
