using System;
using System.Collections.Generic;
using System.Linq;
using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Documents;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// 文档编辑服务（V2 UI/CLI 共用）：对 WorkflowDocument 做结构编辑，
/// 在建边时执行类型兼容、单入线替换与环检测（V2 决策 D7/D8/D14）。
/// 编辑后由调用方触发 Session 的 NotifyNodeChanged 级联。
/// </summary>
public sealed class WorkflowDocumentEditor
{
    private readonly WorkflowDocument _document;
    private readonly WorkflowDefinitionRegistry _registry;
    private readonly ValueTypeRegistry _valueTypes;

    public WorkflowDocumentEditor(
        WorkflowDocument document,
        WorkflowDefinitionRegistry registry,
        ValueTypeRegistry? valueTypes = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(registry);

        _document = document;
        _registry = registry;
        _valueTypes = valueTypes ?? ValueTypeRegistry.CreateDefault();
    }

    public WorkflowDocument Document => _document;

    public NodeDocument AddNode(string typeId, double x = 0, double y = 0)
    {
        if (_registry.TryResolve(typeId) is null)
        {
            throw new KeyNotFoundException($"Node type '{typeId}' is not registered.");
        }

        var node = new NodeDocument
        {
            NodeId = $"node-{Guid.NewGuid():N}",
            NodeTypeId = typeId,
            X = x,
            Y = y
        };

        _document.Nodes.Add(node);
        return node;
    }

    public bool RemoveNode(string nodeId)
    {
        var node = _document.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        if (node is null)
        {
            return false;
        }

        _document.Connections.RemoveAll(connection =>
            string.Equals(connection.SourceNodeId, nodeId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.TargetNodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        return _document.Nodes.Remove(node);
    }

    public bool TryConnect(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId, out string? error)
    {
        var sourceNode = FindNode(sourceNodeId);
        var targetNode = FindNode(targetNodeId);

        if (sourceNode is null)
        {
            error = $"源节点 '{sourceNodeId}' 不存在。";
            return false;
        }

        if (targetNode is null)
        {
            error = $"目标节点 '{targetNodeId}' 不存在。";
            return false;
        }

        if (string.Equals(sourceNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
        {
            error = "节点不能连接到自身。";
            return false;
        }

        var sourceDefinition = _registry.TryResolve(sourceNode.NodeTypeId);
        var targetDefinition = _registry.TryResolve(targetNode.NodeTypeId);

        if (sourceDefinition is null || targetDefinition is null)
        {
            error = "节点类型未注册，无法校验端口。";
            return false;
        }

        var sourcePort = sourceDefinition.OutputPorts.FirstOrDefault(port =>
            string.Equals(port.Id, sourcePortId, StringComparison.OrdinalIgnoreCase));
        var targetPort = targetDefinition.InputPorts.FirstOrDefault(port =>
            string.Equals(port.Id, targetPortId, StringComparison.OrdinalIgnoreCase));

        if (sourcePort is null)
        {
            error = $"源端口 '{sourceNodeId}.{sourcePortId}' 不存在。";
            return false;
        }

        if (targetPort is null)
        {
            error = $"目标端口 '{targetNodeId}.{targetPortId}' 不存在。";
            return false;
        }

        if (!_valueTypes.AreCompatible(sourcePort.TypeId, targetPort.TypeId))
        {
            error = $"端口类型不兼容: {sourcePort.TypeId} -> {targetPort.TypeId}。";
            return false;
        }

        if (WouldCreateCycle(sourceNodeId, targetNodeId))
        {
            error = "该连接会形成环，已被拒绝。";
            return false;
        }

        // D8：同一输入端口新连替换旧连。
        _document.Connections.RemoveAll(connection =>
            string.Equals(connection.TargetNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(connection.TargetPortId, targetPortId, StringComparison.OrdinalIgnoreCase));

        _document.Connections.Add(new ConnectionDocument
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        });

        error = null;
        return true;
    }

    public bool RemoveConnection(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
    {
        var removed = _document.Connections.RemoveAll(connection =>
            string.Equals(connection.SourceNodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(connection.SourcePortId, sourcePortId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(connection.TargetNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(connection.TargetPortId, targetPortId, StringComparison.OrdinalIgnoreCase));

        return removed > 0;
    }

    private NodeDocument? FindNode(string nodeId)
    {
        return _document.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
    }

    private bool WouldCreateCycle(string sourceNodeId, string targetNodeId)
    {
        // 若 source 已可从 target 出发到达，则新增 target -> source 会成环。
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(targetNodeId);
        visited.Add(targetNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var connection in _document.Connections.Where(connection =>
                         string.Equals(connection.SourceNodeId, current, StringComparison.OrdinalIgnoreCase)))
            {
                if (string.Equals(connection.TargetNodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (visited.Add(connection.TargetNodeId))
                {
                    queue.Enqueue(connection.TargetNodeId);
                }
            }
        }

        return false;
    }
}
