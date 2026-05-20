using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Services;

public sealed class WorkflowEventHub
{
    public event EventHandler<NodeStatusChangedEventArgs>? NodeStatusChanged;

    public event EventHandler<PortValueChangedEventArgs>? PortValueChanged;

    public void PublishNodeStatusChanged(string nodeId, NodeStatus status)
    {
        NodeStatusChanged?.Invoke(this, new NodeStatusChangedEventArgs(nodeId, status));
    }

    public void PublishPortValueChanged(string nodeId, string portId, object? value, PortStatus status)
    {
        PortValueChanged?.Invoke(this, new PortValueChangedEventArgs(nodeId, portId, value, status));
    }
}
