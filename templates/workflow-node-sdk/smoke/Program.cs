using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using WorkflowStudioSdkNode;

var registry = new WorkflowDefinitionRegistry();
registry.Register(
    "sample.workflow-node",
    new SampleNode(),
    new NodeTypeDescriptor(
        "sample.workflow-node",
        "工作流示例节点",
        "Sdk",
        SettingsFields: WorkflowDefinitionRegistry.ExtractSettingsFields(typeof(WorkflowStudioSdkNodeSettings))));

var document = new WorkflowDocument();
var node = new NodeDocument { NodeId = "demo", NodeTypeId = "sample.workflow-node" };
node.Settings["suffix"] = "ok";
document.Nodes.Add(node);

var session = new WorkflowSession(document, registry.CreateResolver());
var result = await new WorkflowExecutor(session, maxConcurrency: 1).ExecuteAllAsync();

Console.WriteLine(result.HasFailures ? "FAIL" : "OK");
return result.HasFailures ? 1 : 0;
