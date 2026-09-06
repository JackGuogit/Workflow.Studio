using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Workflow.Studio.Core.Catalog;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// V2 就绪队列执行引擎（V2 架构文档 6 节，M3）。
/// 支持执行全部/执行到节点（clean 复用）/从节点重跑下游；
/// 失败节点下游 Blocked，独立分支继续；可取消。
/// 元节点容器递归由 M5 接入；断点暂停门由后续调试切片接入。
/// </summary>
public sealed class WorkflowExecutor
{
    private readonly WorkflowSession _session;
    private readonly int _maxConcurrency;
    private readonly Func<NodeRuntime, CancellationToken, Task>? _breakpointGate;

    public WorkflowExecutor(
        WorkflowSession session,
        int? maxConcurrency = null,
        Func<NodeRuntime, CancellationToken, Task>? breakpointGate = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _maxConcurrency = Math.Max(1, maxConcurrency ?? Environment.ProcessorCount);
        _breakpointGate = breakpointGate;
    }

    public int MaxConcurrency => _maxConcurrency;

    public Task<WorkflowExecutionResult> ExecuteAllAsync(CancellationToken cancellationToken = default)
    {
        var marked = _session.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ExecuteCoreAsync(marked, forceRerun: marked, cancellationToken);
    }

    public async Task<WorkflowExecutionResult> ExecuteUpToAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var target = _session.GetNode(nodeId);

        if (target.State == NodeState.Succeeded)
        {
            // 目标已执行且 clean：无动作（V2 决策 R8，clean 复用）。
            return new WorkflowExecutionResult(false, [], [], [], []);
        }

        var marked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MarkUpstreamAsNeeded(target, marked);

        return await ExecuteCoreAsync(marked, forceRerun: null, cancellationToken);
    }

    public async Task<WorkflowExecutionResult> ExecuteFromAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var target = _session.GetNode(nodeId);

        var force = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectDownstream(target, force);

        // 保证被强制重跑节点的上游可用：Succeeded 的直接复用，未执行的补跑。
        var marked = new HashSet<string>(force, StringComparer.OrdinalIgnoreCase);
        foreach (var forceNodeId in force)
        {
            MarkUpstreamAsNeeded(_session.GetNode(forceNodeId), marked);
        }

        return await ExecuteCoreAsync(marked, force, cancellationToken);
    }

    private void MarkUpstreamAsNeeded(NodeRuntime node, HashSet<string> marked)
    {
        if (node.State == NodeState.Succeeded || !marked.Add(node.NodeId))
        {
            return;
        }

        foreach (var inputPort in node.InputPorts)
        {
            if (_session.TryGetIncomingSource(node.NodeId, inputPort.PortId, out var sourceNode, out _)
                && sourceNode is not null)
            {
                MarkUpstreamAsNeeded(sourceNode, marked);
            }
        }
    }

    private void CollectDownstream(NodeRuntime start, HashSet<string> marked)
    {
        var queue = new Queue<NodeRuntime>();
        queue.Enqueue(start);
        marked.Add(start.NodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var (targetNodeId, _) in _session.GetOutgoingConnections(current.NodeId))
            {
                if (marked.Add(targetNodeId))
                {
                    queue.Enqueue(_session.GetNode(targetNodeId));
                }
            }
        }
    }

    private async Task<WorkflowExecutionResult> ExecuteCoreAsync(
        IReadOnlyCollection<string> markedIds,
        IReadOnlyCollection<string>? forceRerun,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(markedIds);

        var executedNodeIds = new List<string>();
        var failedNodeIds = new List<string>();
        var blockedNodeIds = new List<string>();
        var canceledNodeIds = new List<string>();

        // 强制重跑的节点清掉旧成功态，随后统一重新 Configure。
        if (forceRerun is not null)
        {
            foreach (var nodeId in forceRerun)
            {
                var node = _session.GetNode(nodeId);
                if (node.State == NodeState.Succeeded)
                {
                    node.SetState(NodeState.NotConfigured);
                }
            }
        }

        _session.ConfigureAll();

        // 配置失败的标记节点：立即失败并阻塞其下游（此时尚未计算待执行集合）。
        foreach (var nodeId in markedIds)
        {
            var node = _session.GetNode(nodeId);
            if (node.State == NodeState.NotConfigured)
            {
                node.SetState(NodeState.Failed);
                failedNodeIds.Add(nodeId);
            }
        }

        foreach (var failedNodeId in failedNodeIds.ToList())
        {
            BlockDownstream(_session.GetNode(failedNodeId), markedIds, blockedNodeIds);
        }

        var toExecute = markedIds
            .Select(_session.GetNode)
            .Where(node => node.State == NodeState.Configured)
            .ToList();

        var toExecuteSet = toExecute
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pendingCount = toExecute.Count;
        var ready = new Queue<NodeRuntime>();
        var prerequisiteRemaining = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in toExecute)
        {
            var count = 0;

            foreach (var inputPort in node.InputPorts)
            {
                if (_session.TryGetIncomingSource(node.NodeId, inputPort.PortId, out var sourceNode, out _)
                    && sourceNode is not null
                    && toExecuteSet.Contains(sourceNode.NodeId))
                {
                    count++;
                }
            }

            prerequisiteRemaining[node.NodeId] = count;
            if (count == 0)
            {
                ready.Enqueue(node);
            }
        }

        var running = new List<Task<NodeCompletion>>();
        var canceled = false;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        while (pendingCount > 0 && !canceled)
        {
            while (running.Count < _maxConcurrency && ready.Count > 0)
            {
                var node = ready.Dequeue();
                if (node.State != NodeState.Configured)
                {
                    continue;
                }

                running.Add(ExecuteNodeGuardedAsync(
                    node,
                    linkedCancellation.Token));
            }

            if (running.Count == 0)
            {
                // 无就绪节点可调度（不应出现，防御性退出）。
                break;
            }

            var finished = await Task.WhenAny(running);
            running.Remove(finished);

            var completedNode = finished.Result.Node;
            var outcome = finished.Result.Outcome;
            pendingCount--;

            switch (outcome)
            {
                case NodeCompletionOutcome.Succeeded:
                    executedNodeIds.Add(completedNode.NodeId);
                    NotifySuccessors(completedNode.NodeId, prerequisiteRemaining, ready, toExecuteSet);
                    break;
                case NodeCompletionOutcome.Failed:
                    failedNodeIds.Add(completedNode.NodeId);
                    pendingCount -= BlockDownstream(completedNode, markedIds, blockedNodeIds);
                    break;
                case NodeCompletionOutcome.Canceled:
                    canceledNodeIds.Add(completedNode.NodeId);
                    canceled = true;
                    break;
            }

            canceled = canceled || linkedCancellation.IsCancellationRequested;
        }

        // 取消后等在途任务退出（其取消处理在 guarded 任务内完成）。
        if (running.Count > 0)
        {
            var outcomes = await Task.WhenAll(running);

            foreach (var outcome in outcomes)
            {
                if (outcome.Outcome == NodeCompletionOutcome.Canceled)
                {
                    canceled = true;
                }
            }
        }

        return new WorkflowExecutionResult(
            canceled,
            executedNodeIds,
            failedNodeIds,
            blockedNodeIds,
            canceledNodeIds);
    }

    private async Task<NodeCompletion> ExecuteNodeGuardedAsync(
        NodeRuntime node,
        CancellationToken cancellationToken)
    {
        if (!_session.TryResolveSettings(node, out var resolvedSettings, out var settingsError))
        {
            return Fail(node, settingsError!);
        }

        if (!_session.TryResolveVisibleFlowVariables(node, out var visibleVariables, out var visibleError))
        {
            return Fail(node, visibleError!);
        }

        if (_breakpointGate is not null && node.SourceDocument.IsBreakpointEnabled)
        {
            node.SetState(NodeState.Paused);

            try
            {
                await _breakpointGate(node, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                node.SetState(NodeState.Failed);
                node.SetLastError("断点等待被取消。");
                return new NodeCompletion(node, NodeCompletionOutcome.Canceled);
            }
        }

        node.SetState(NodeState.Running);

        var inputValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var inputPort in node.InputPorts)
        {
            if (_session.TryGetIncomingSource(node.NodeId, inputPort.PortId, out var sourceNode, out var sourcePortId)
                && sourceNode is not null)
            {
                if (sourceNode.TryReadOutputValue(sourcePortId, out var value))
                {
                    inputValues[inputPort.PortId] = value;
                }
                else
                {
                    return Fail(node, "上游输出不可用（未执行或已被失效）。");
                }
            }
            else if (!inputPort.IsOptional)
            {
                if (_session.TryGetExternalInputValue(node.NodeId, inputPort.PortId, out var externalValue))
                {
                    inputValues[inputPort.PortId] = externalValue;
                }
                else
                {
                    return Fail(node, "缺少必连输入值。");
                }
            }
        }

        NodeExecutionResult result;

        try
        {
            result = await node.Definition.ExecuteAsync(new NodeExecutionRequest
            {
                SourceDocument = node.SourceDocument,
                NodePath = node.Path,
                InputValues = inputValues,
                Settings = resolvedSettings,
                Variables = visibleVariables,
                DeclaredVariables = _session.DeclaredVariableValues
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            node.SetState(NodeState.Failed);
            node.SetLastError("执行已取消。");
            return new NodeCompletion(node, NodeCompletionOutcome.Canceled);
        }
        catch (Exception ex)
        {
            return Fail(node, ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            return Fail(node, result.Error!);
        }

        var missingOutputs = node.OutputPorts
            .Where(port => !result.OutputValues.ContainsKey(port.PortId))
            .Select(port => port.PortId)
            .ToList();

        if (missingOutputs.Count > 0)
        {
            return Fail(node, $"节点未提供输出值: {string.Join(", ", missingOutputs)}。");
        }

        var flowVariableError = ValidateAndStoreFlowVariables(node, result.OutputVariables);
        if (flowVariableError is not null)
        {
            return Fail(node, flowVariableError);
        }

        node.SetLastError(null);
        node.SetState(NodeState.Succeeded);

        foreach (var outputPort in node.OutputPorts)
        {
            node.TryPublishOutputValue(outputPort.PortId, result.OutputValues[outputPort.PortId]);
        }

        return new NodeCompletion(node, NodeCompletionOutcome.Succeeded);
    }

    private string? ValidateAndStoreFlowVariables(
        NodeRuntime node,
        IReadOnlyDictionary<string, object?> produced)
    {
        var declarations = node.Definition.OutputVariables;

        if (declarations.Count == 0)
        {
            if (produced.Count > 0)
            {
                return $"节点产出了未声明的流变量: {string.Join(", ", produced.Keys)}。";
            }

            return null;
        }

        foreach (var declaration in declarations)
        {
            if (!produced.TryGetValue(declaration.Name, out var value))
            {
                return $"缺少声明的流变量 '{declaration.Name}'。";
            }

            if (!TryValidateVariableValue(node, declaration, value, out var typeError))
            {
                return typeError;
            }
        }

        foreach (var key in produced.Keys)
        {
            if (declarations.All(declaration =>
                    !string.Equals(declaration.Name, key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"产出了未声明的流变量 '{key}'。";
            }
        }

        node.SetProducedFlowVariables(produced);
        return null;
    }

    private bool TryValidateVariableValue(
        NodeRuntime node,
        FlowVariableDeclaration declaration,
        object? value,
        out string? error)
    {
        ValueTypeDefinition valueType;

        try
        {
            valueType = _session.ValueTypes.Get(declaration.TypeId);
        }
        catch (KeyNotFoundException)
        {
            error = $"节点 '{node.NodeId}' 声明的流变量 '{declaration.Name}' 引用了未注册类型 '{declaration.TypeId}'。";
            return false;
        }

        if (value is null)
        {
            if (valueType.PayloadType.IsValueType)
            {
                error = $"流变量 '{declaration.Name}' 的类型 '{valueType.DisplayName}' 不允许空值。";
                return false;
            }

            error = null;
            return true;
        }

        if (!valueType.PayloadType.IsInstanceOfType(value))
        {
            error = $"流变量 '{declaration.Name}' 期望类型 '{valueType.DisplayName}'，但实际产出 '{value.GetType().Name}'。";
            return false;
        }

        error = null;
        return true;
    }

    private void NotifySuccessors(
        string completedNodeId,
        Dictionary<string, int> prerequisiteRemaining,
        Queue<NodeRuntime> ready,
        HashSet<string> toExecuteSet)
    {
        foreach (var (targetNodeId, _) in _session.GetOutgoingConnections(completedNodeId))
        {
            if (!toExecuteSet.Contains(targetNodeId) || !prerequisiteRemaining.TryGetValue(targetNodeId, out var remaining))
            {
                continue;
            }

            remaining--;
            prerequisiteRemaining[targetNodeId] = remaining;

            if (remaining == 0)
            {
                var target = _session.GetNode(targetNodeId);
                if (target.State == NodeState.Configured)
                {
                    ready.Enqueue(target);
                }
            }
        }
    }

    /// <summary>
    /// 把失败节点的传递下游（限于本次标记集合内）置为 Blocked。
    /// 返回新阻塞节点数，供调用方扣减未决计数。
    /// </summary>
    private int BlockDownstream(NodeRuntime failedNode, IReadOnlyCollection<string> markedIds, List<string> blockedNodeIds)
    {
        var newlyBlocked = 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<NodeRuntime>();
        queue.Enqueue(failedNode);
        visited.Add(failedNode.NodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var (targetNodeId, _) in _session.GetOutgoingConnections(current.NodeId))
            {
                if (!markedIds.Contains(targetNodeId) || !visited.Add(targetNodeId))
                {
                    continue;
                }

                var target = _session.GetNode(targetNodeId);
                if (target.State is NodeState.Succeeded or NodeState.Failed or NodeState.Blocked or NodeState.NotConfigured)
                {
                    continue;
                }

                target.SetState(NodeState.Blocked);
                target.SetLastError($"依赖的节点 '{failedNode.NodeId}' 执行失败。");
                blockedNodeIds.Add(targetNodeId);
                newlyBlocked++;
                queue.Enqueue(target);
            }
        }

        return newlyBlocked;
    }

    private NodeCompletion Fail(NodeRuntime node, string message)
    {
        node.SetLastError(message);
        node.SetState(NodeState.Failed);
        return new NodeCompletion(node, NodeCompletionOutcome.Failed);
    }

    private sealed record NodeCompletion(NodeRuntime Node, NodeCompletionOutcome Outcome);

    private enum NodeCompletionOutcome
    {
        Succeeded,
        Failed,
        Canceled
    }
}

public sealed record WorkflowExecutionResult(
    bool IsCanceled,
    IReadOnlyList<string> ExecutedNodeIds,
    IReadOnlyList<string> FailedNodeIds,
    IReadOnlyList<string> BlockedNodeIds,
    IReadOnlyList<string> CanceledNodeIds)
{
    public bool HasFailures => FailedNodeIds.Count > 0 || CanceledNodeIds.Count > 0;
}
