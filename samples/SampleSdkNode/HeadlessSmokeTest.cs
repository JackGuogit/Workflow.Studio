using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Session;
using SampleSdkNode;

namespace SampleSdkNode.Smoke;

internal static class Program
{
    public static async Task<int> Main()
    {
        var registry = new WorkflowDefinitionRegistry();
        var settingsType = typeof(SampleTextSourceSettings);

        registry.Register(
            SampleTextSourceNode.TypeId,
            new SampleTextSourceNode(),
            new NodeTypeDescriptor(
                SampleTextSourceNode.TypeId,
                "SDK 文本源",
                "Sdk",
                SettingsFields: WorkflowDefinitionRegistry.ExtractSettingsFields(settingsType)));

        var fields = registry.Descriptors.Single().SettingsFields;
        if (fields.Count != 3)
        {
            Console.Error.WriteLine("FAIL: 期望从 [WorkflowSetting] 提取 3 个字段。");
            return 1;
        }

        if (fields.First(field => field.Key == "Mode").Options is not { Count: 3 })
        {
            Console.Error.WriteLine("FAIL: 期望 Mode 字段包含枚举选项。");
            return 1;
        }

        var document = new WorkflowDocument();
        var node = new NodeDocument
        {
            NodeId = "source",
            NodeTypeId = SampleTextSourceNode.TypeId
        };
        node.Settings["Text"] = "smoke";
        document.Nodes.Add(node);

        var session = new WorkflowSession(document, registry.CreateResolver());
        var executor = new WorkflowExecutor(session, maxConcurrency: 1);
        var result = await executor.ExecuteAllAsync();

        if (result.HasFailures || session.GetNode("source").ProducedFlowVariables?["sampleText"] as string != "smoke")
        {
            Console.Error.WriteLine("FAIL: 执行结果与预期不符。");
            return 1;
        }

        Console.WriteLine("OK: 字段元数据提取与无头执行通过。");

        var loaderRegistry = new WorkflowDefinitionRegistry();
        var pluginDirectory = Path.GetDirectoryName(typeof(SampleTextSourceNode).Assembly.Location)!;
        var loaded = ExternalNodeLoader.LoadFromDirectory(loaderRegistry, pluginDirectory);
        if (!loaded.Contains(SampleTextSourceNode.TypeId) || loaderRegistry.TryResolve(SampleTextSourceNode.TypeId) is null)
        {
            Console.Error.WriteLine("FAIL: 扩展节点目录加载未找到 SDK 示例节点。");
            return 1;
        }

        Console.WriteLine("OK: 扩展节点目录（AssemblyLoadContext）加载通过。");

        var pluginCatalog = new PluginCatalog();
        var loadedPlugins = ExternalNodeLoader.LoadPluginsFromDirectory(pluginCatalog, pluginDirectory);
        if (!loadedPlugins.Contains("sample.capability") || pluginCatalog.Plugins.Count != 1)
        {
            Console.Error.WriteLine("FAIL: 插件目录加载未找到示例能力插件。");
            return 1;
        }

        await pluginCatalog.InitializeAsync();
        Console.WriteLine("OK: 插件（能力）目录与节点目录分离加载通过。");
        await pluginCatalog.DisposeAsync();
        return 0;
    }
}
