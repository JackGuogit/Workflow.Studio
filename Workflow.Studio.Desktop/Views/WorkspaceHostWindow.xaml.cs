using System.Windows;
using System.IO;
using System.Collections.Generic;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Desktop.Services;
using Workflow.Studio.Plugins.BuiltIn;
using Workflow.Studio.Workbench.Editor;

namespace Workflow.Studio.Desktop.Views;

public partial class WorkspaceHostWindow : Window
{
    public WorkspaceHostWindow()
    {
        InitializeComponent();

        var registry = EditorHostFactory.BuildRegistry();
        EditorHostFactory.LoadExtensionNodes(registry);

        var pluginCatalog = new PluginCatalog();
        pluginCatalog.Register(new OpenCvImageCodecPlugin());
        pluginCatalog.Register(new OpenCvImageProcessingPlugin());
        pluginCatalog.InitializeAsync().AsTask().GetAwaiter().GetResult();

        var externalPluginDirectories = new List<string>();
        try
        {
            var sampleDir = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "SampleSdkNode", "bin", "Debug", "net10.0"));
            if (Directory.Exists(sampleDir))
            {
                externalPluginDirectories.Add(sampleDir);
                ExternalNodeLoader.LoadPluginsFromDirectory(pluginCatalog, sampleDir);
            }
        }
        catch
        {
            // 示例产物缺失时跳过，不影响启动。
        }

        var workspace = new EditorWorkspaceViewModel(
            registry,
            new WorkflowDocumentPickerService(),
            pluginCatalog,
            externalPluginDirectories);
        workspace.AddDocument(EditorHostFactory.CreateDemoDocument(), "演示工作流", maxConcurrency: 4);
        workspace.AddDocument(new WorkflowDocument(), "未命名工作流", maxConcurrency: 4);

        DataContext = workspace;
    }
}
