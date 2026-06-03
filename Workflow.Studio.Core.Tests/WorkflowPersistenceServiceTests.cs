using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Services;
using Workflow.Studio.Nodes;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowPersistenceServiceTests
{
    [Fact]
    public async Task SaveAndLoadAsync_ShouldPersistBreakpoints()
    {
        var nodeManager = new NodeManager();
        nodeManager.RegisterType(new TextSourceNode());
        nodeManager.RegisterType(new CsvReadNode());

        var nodeFactory = new NodeFactory(nodeManager);
        var persistenceService = new WorkflowPersistenceService(nodeFactory, new WorkflowConnectionValidator());
        var workflow = new WorkflowData();

        var source = nodeFactory.CreateNode("demo.node.text-source", 10, 20, "source");
        var csv = nodeFactory.CreateNode("demo.node.csv-read", 30, 40, "csv");
        source.SetBreakpointEnabled(true);
        workflow.AddNode(source);
        workflow.AddNode(csv);

        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.workflow.json");

        try
        {
            await persistenceService.SaveAsync(workflow, filePath);
            var restored = await persistenceService.LoadAsync(filePath);

            Assert.Equal(2, restored.Nodes.Count);
            Assert.True(restored.Nodes.First(node => node.Metadata.Id == "source").IsBreakpointEnabled);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
