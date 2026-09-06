using Workflow.Studio.Core.Documents;
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
        if (fields.Count != 2)
        {
            Console.Error.WriteLine("FAIL: 期望从 [WorkflowSetting] 提取 2 个字段。");
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
        return 0;
    }
}
