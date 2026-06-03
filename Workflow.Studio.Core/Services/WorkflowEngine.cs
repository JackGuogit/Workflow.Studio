using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Nodes;
using CoreExecutionContext = Workflow.Studio.Core.Runtime.ExecutionContext;

namespace Workflow.Studio.Core.Services;

public sealed class WorkflowEngine
{
    private readonly PluginManager _pluginManager;
    private readonly NodeManager _nodeManager;
    private readonly WorkflowEventHub _eventHub;
    private readonly IWorkflowConnectionValidator _connectionValidator;
    private readonly IWorkflowDebugController _debugController;
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    public WorkflowEngine(
        PluginManager pluginManager,
        NodeManager nodeManager,
        WorkflowEventHub eventHub,
        IWorkflowConnectionValidator connectionValidator,
        IWorkflowDebugController debugController)
    {
        _pluginManager = pluginManager;
        _nodeManager = nodeManager;
        _eventHub = eventHub;
        _connectionValidator = connectionValidator;
        _debugController = debugController;
    }

    public event EventHandler<NodeStatusChangedEventArgs>? NodeStatusChanged;

    public event EventHandler<PortValueChangedEventArgs>? PortValueChanged;

    public async Task<CoreExecutionContext> ExecuteAsync(WorkflowData workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var enteredExecutionGate = false;

        try
        {
            enteredExecutionGate = await _executionGate.WaitAsync(0, cancellationToken);
            if (!enteredExecutionGate)
            {
                throw new InvalidOperationException("Workflow engine is already executing another workflow.");
            }

            _nodeManager.AttachWorkflow(workflow);
            _connectionValidator.EnsureWorkflowIsValid(workflow);
            ResetWorkflowState(workflow);
            _debugController.StartSession();
            _debugController.EmitLog(WorkflowLogLevel.Info, $"准备执行工作流，节点数 {workflow.Nodes.Count}，连线数 {workflow.Connections.Count}。");
            await _pluginManager.InitializeAsync(cancellationToken);

            var context = new CoreExecutionContext(workflow.GlobalVariables);
            var executionBatches = BuildExecutionBatches(workflow);

            for (var batchIndex = 0; batchIndex < executionBatches.Count; batchIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = executionBatches[batchIndex];
                _debugController.EmitLog(WorkflowLogLevel.Debug, $"开始执行批次 {batchIndex + 1}/{executionBatches.Count}，节点数 {batch.Count}。");

                var batchTasks = batch
                    .Select(node => ExecuteNodeAsync(workflow, node, context, cancellationToken))
                    .ToArray();

                await Task.WhenAll(batchTasks);
            }

            SyncGlobalVariables(workflow, context);
            _debugController.EmitLog(WorkflowLogLevel.Info, $"执行完成，共生成 {context.History.Count} 条节点记录。");
            return context;
        }
        catch (Exception ex)
        {
            _debugController.EmitLog(WorkflowLogLevel.Error, $"执行中断：{ex.Message}");
            throw;
        }
        finally
        {
            _debugController.CompleteSession();
            if (enteredExecutionGate)
            {
                _executionGate.Release();
            }
        }
    }

    public IReadOnlyList<NodeData> BuildExecutionPlan(WorkflowData workflow)
    {
        return BuildExecutionBatches(workflow).SelectMany(batch => batch).ToList();
    }

    public IReadOnlyList<IReadOnlyList<NodeData>> BuildExecutionBatches(WorkflowData workflow)
    {
        var indegree = workflow.Nodes.ToDictionary(node => node.Metadata.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var adjacency = workflow.Nodes.ToDictionary(
            node => node.Metadata.Id,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var connection in workflow.Connections)
        {
            indegree[connection.TargetNodeId]++;
            adjacency[connection.SourceNodeId].Add(connection.TargetNodeId);
        }

        var queue = new Queue<NodeData>(
            workflow.Nodes
                .Where(node => indegree[node.Metadata.Id] == 0)
                .OrderBy(node => node.Metadata.Name, StringComparer.OrdinalIgnoreCase));
        var visitedCount = 0;
        var batches = new List<IReadOnlyList<NodeData>>();

        while (queue.Count > 0)
        {
            var batchSize = queue.Count;
            var currentBatch = new List<NodeData>(batchSize);

            for (var index = 0; index < batchSize; index++)
            {
                var current = queue.Dequeue();
                currentBatch.Add(current);
                visitedCount++;

                foreach (var targetNodeId in adjacency[current.Metadata.Id])
                {
                    indegree[targetNodeId]--;

                    if (indegree[targetNodeId] == 0)
                    {
                        queue.Enqueue(_nodeManager.GetNode(targetNodeId));
                    }
                }
            }

            batches.Add(currentBatch);
        }

        if (visitedCount != workflow.Nodes.Count)
        {
            throw new InvalidOperationException("Workflow contains a cycle and cannot be executed as a DAG.");
        }

        return batches;
    }

    private static IReadOnlyDictionary<string, object?> SnapshotPorts(IEnumerable<PortData> ports)
    {
        return ports.ToDictionary(port => port.Metadata.Id, port => port.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task ExecuteNodeAsync(
        WorkflowData workflow,
        NodeData node,
        CoreExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var inputSnapshot = SnapshotPorts(node.InputPorts);

        try
        {
            if (node.IsBreakpointEnabled)
            {
                UpdateNodeStatus(node, NodeStatus.Paused);
                await _debugController.PauseAtBreakpointAsync(node, cancellationToken);
            }

            UpdateNodeStatus(node, NodeStatus.Running);
            _debugController.EmitLog(WorkflowLogLevel.Info, $"开始执行节点: {node.Metadata.Name}", node);

            var nodeType = _nodeManager.GetNodeType(node.NodeTypeId);
            var request = new NodeExecutionRequest
            {
                Node = node,
                InputValues = inputSnapshot
            };

            var result = await nodeType.ExecuteAsync(request, context, cancellationToken);
            ApplyNodeResult(node, result, context);
            PropagateOutputs(workflow, node, context);

            UpdateNodeStatus(node, NodeStatus.Success);
            node.SetLastMessage(result.Message);
            node.SetLastError(null);
            CaptureExecutionArtifacts(node, result.Message, startedAt, inputSnapshot, context);
            _debugController.EmitLog(
                WorkflowLogLevel.Info,
                string.IsNullOrWhiteSpace(result.Message) ? $"节点执行成功: {node.Metadata.Name}" : result.Message,
                node);
        }
        catch (Exception ex)
        {
            UpdateNodeStatus(node, NodeStatus.Failed);
            MarkPortsFailed(node);
            node.SetLastError(ex.Message);
            node.SetLastMessage(null);
            CaptureExecutionArtifacts(node, ex.Message, startedAt, inputSnapshot, context);
            _debugController.EmitLog(WorkflowLogLevel.Error, ex.Message, node);
            throw;
        }
    }

    private void CaptureExecutionArtifacts(
        NodeData node,
        string? message,
        DateTimeOffset startedAt,
        IReadOnlyDictionary<string, object?> inputSnapshot,
        CoreExecutionContext context)
    {
        var outputSnapshot = SnapshotPorts(node.OutputPorts);

        context.AddRecord(new NodeExecutionRecord
        {
            NodeId = node.Metadata.Id,
            NodeName = node.Metadata.Name,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Status = node.Status,
            Message = message,
            InputSnapshot = inputSnapshot,
            OutputSnapshot = outputSnapshot
        });

        context.CaptureNodeSnapshot(node.Metadata.Id, node.Status, inputSnapshot, outputSnapshot);
    }

    private void ApplyNodeResult(NodeData node, NodeExecutionResult result, CoreExecutionContext context)
    {
        foreach (var outputPort in node.OutputPorts)
        {
            if (!result.OutputValues.TryGetValue(outputPort.Metadata.Id, out var value))
            {
                continue;
            }

            outputPort.SetValue(value, BuildPortContext(node, outputPort));
            context.CapturePortValue(node.Metadata.Id, outputPort.Metadata.Id, value);
            RaisePortValueChanged(node.Metadata.Id, outputPort);
        }

        foreach (var globalVariable in result.GlobalVariables)
        {
            context.GlobalVariables[globalVariable.Key] = globalVariable.Value;
        }
    }

    private void PropagateOutputs(WorkflowData workflow, NodeData sourceNode, CoreExecutionContext context)
    {
        foreach (var connection in _nodeManager.GetOutgoingConnections(workflow, sourceNode.Metadata.Id))
        {
            var sourcePort = _nodeManager.GetPort(connection.SourceNodeId, connection.SourcePortId);
            var targetNode = _nodeManager.GetNode(connection.TargetNodeId);
            var targetPort = _nodeManager.GetPort(connection.TargetNodeId, connection.TargetPortId);

            targetPort.SetValue(sourcePort.Value, BuildPortContext(targetNode, targetPort));
            context.CapturePortValue(connection.TargetNodeId, connection.TargetPortId, sourcePort.Value);
            RaisePortValueChanged(connection.TargetNodeId, targetPort);
        }
    }

    private void ResetWorkflowState(WorkflowData workflow)
    {
        foreach (var node in workflow.Nodes)
        {
            node.ClearRuntimeState();

            foreach (var port in node.InputPorts.Concat(node.OutputPorts))
            {
                port.Clear();
            }
        }

        foreach (var connection in workflow.Connections)
        {
            _nodeManager.GetPort(connection.SourceNodeId, connection.SourcePortId).MarkConnected();
            _nodeManager.GetPort(connection.TargetNodeId, connection.TargetPortId).MarkConnected();
        }
    }

    private void UpdateNodeStatus(NodeData node, NodeStatus status)
    {
        node.SetStatus(status);
        _eventHub.PublishNodeStatusChanged(node.Metadata.Id, status);
        NodeStatusChanged?.Invoke(this, new NodeStatusChangedEventArgs(node.Metadata.Id, status));
    }

    private void RaisePortValueChanged(string nodeId, PortData port)
    {
        _eventHub.PublishPortValueChanged(nodeId, port.Metadata.Id, port.Value, port.Status);
        PortValueChanged?.Invoke(
            this,
            new PortValueChangedEventArgs(
                nodeId,
                port.Metadata.Id,
                port.Value,
                port.Status));
    }

    private static void MarkPortsFailed(NodeData node)
    {
        foreach (var port in node.InputPorts.Concat(node.OutputPorts))
        {
            port.MarkFailed();
        }
    }

    private static void SyncGlobalVariables(WorkflowData workflow, CoreExecutionContext context)
    {
        workflow.GlobalVariables.Clear();

        foreach (var entry in context.GlobalVariables)
        {
            workflow.GlobalVariables[entry.Key] = entry.Value;
        }
    }

    private static string BuildPortContext(NodeData node, PortData port)
    {
        return $"端口 '{node.Metadata.Name}.{port.Metadata.Name}'";
    }
}

public sealed class NodeStatusChangedEventArgs : EventArgs
{
    public NodeStatusChangedEventArgs(string nodeId, NodeStatus status)
    {
        NodeId = nodeId;
        Status = status;
    }

    public string NodeId { get; }

    public NodeStatus Status { get; }
}

public sealed class PortValueChangedEventArgs : EventArgs
{
    public PortValueChangedEventArgs(string nodeId, string portId, object? value, PortStatus status)
    {
        NodeId = nodeId;
        PortId = portId;
        Value = value;
        Status = status;
    }

    public string NodeId { get; }

    public string PortId { get; }

    public object? Value { get; }

    public PortStatus Status { get; }
}
