using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using Workflow.Studio.Core.Services;
using Workflow.Studio.Nodes;
using Workflow.Studio.Plugins.BuiltIn;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowEngineDebugTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPauseAndResumeWhenBreakpointEnabled()
    {
        var pluginManager = new PluginManager();
        pluginManager.Register(new UppercaseTransformPlugin());
        pluginManager.Register(new PreviewPlugin());

        var nodeManager = new NodeManager();
        nodeManager.RegisterType(new TextSourceNode());
        nodeManager.RegisterType(new UppercaseTransformNode(new UppercaseTransformPlugin()));

        var eventHub = new WorkflowEventHub();
        var debugController = new WorkflowDebugController();
        var engine = new WorkflowEngine(pluginManager, nodeManager, eventHub, new WorkflowConnectionValidator(), debugController);
        var workflow = new WorkflowData();

        var sourceNode = new NodeFactory(nodeManager).CreateNode("demo.node.text-source", 0, 0, "source");
        sourceNode.SetBreakpointEnabled(true);
        workflow.AddNode(sourceNode);

        var breakpointHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        debugController.BreakpointHit += (_, _) => breakpointHit.TrySetResult(true);

        var executeTask = engine.ExecuteAsync(workflow);

        await breakpointHit.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(debugController.IsPaused);

        debugController.Resume();
        var context = await executeTask;

        Assert.Single(context.History);
        Assert.Equal(NodeStatus.Success, sourceNode.Status);
    }
}
