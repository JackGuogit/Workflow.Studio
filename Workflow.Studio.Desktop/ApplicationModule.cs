using Autofac;
using Workflow.Studio.Desktop.ViewModels;
using Workflow.Studio.Core.Nodes.BuiltIn;
using Workflow.Studio.Core.Plugins.BuiltIn;
using Workflow.Studio.Core.Services;
using Workflow.Studio.Workbench.ViewModels;

namespace Workflow.Studio.Desktop;

public sealed class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(_ =>
            {
                var nodeManager = _.Resolve<NodeManager>();
                var pluginManager = new PluginManager(nodeManager);
                pluginManager.Register(new UppercaseTransformPlugin());
                pluginManager.Register(new PreviewPlugin());
                pluginManager.InitializeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
                return pluginManager;
            })
            .SingleInstance();

        builder.Register(_ =>
            {
                var nodeManager = new NodeManager();
                nodeManager.RegisterType(new TextSourceNode());
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
