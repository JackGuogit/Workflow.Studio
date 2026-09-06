using System;
using System.Collections.Generic;
using System.Linq;
using Workflow.Studio.Core.Documents;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// 把同一容器内的一组节点打包为元节点（V2 决策 D17/G3 的 Core 部分）：
/// 生成边界伪节点（Inlet/Outlet）与内部边，外层跨边界连接改写为指向/来自元节点端口。
/// </summary>
public static class WorkflowMetanodePacker
{
    public static NodeDocument Pack(
        WorkflowDocument document,
        WorkflowDefinitionRegistry registry,
        IReadOnlyCollection<string> selectedNodeIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(selectedNodeIds);

        if (selectedNodeIds.Count == 0)
        {
            throw new ArgumentException("至少选择一个节点。", nameof(selectedNodeIds));
        }

        var selected = new HashSet<string>(selectedNodeIds, StringComparer.OrdinalIgnoreCase);
        var selectedNodes = document.Nodes
            .Where(node => selected.Contains(node.NodeId))
            .ToList();

        if (selectedNodes.Count != selectedNodeIds.Count)
        {
            throw new InvalidOperationException("存在无法找到的节点。");
        }

        if (selectedNodes.Any(node => node.InnerWorkflow is not null))
        {
            throw new NotSupportedException("暂不支持把已有元节点再次打包。");
        }

        var metaNodeId = $"meta-{Guid.NewGuid():N}";
        var inner = new WorkflowDocument();
        var existingIds = new HashSet<string>(
            document.Nodes.Select(node => node.NodeId),
            StringComparer.OrdinalIgnoreCase);
        var boundaryCounter = 0;

        foreach (var node in selectedNodes)
        {
            var clone = new NodeDocument
            {
                NodeId = node.NodeId,
                NodeTypeId = node.NodeTypeId,
                X = node.X - (selectedNodes.Min(item => item.X) - 60),
                Y = node.Y - (selectedNodes.Min(item => item.Y) - 60),
                IsBreakpointEnabled = node.IsBreakpointEnabled,
                Settings = node.Settings,
                SettingsBindings = node.SettingsBindings.ToList(),
                VariableMappings = node.VariableMappings.ToList(),
                Ports = node.Ports.ToList(),
                InnerWorkflow = node.InnerWorkflow
            };

            inner.Nodes.Add(clone);
        }

        var internalEdges = document.Connections
            .Where(connection =>
                selected.Contains(connection.SourceNodeId) && selected.Contains(connection.TargetNodeId))
            .Select(CloneConnection)
            .ToList();
        inner.Connections.AddRange(internalEdges);

        var crossingEdges = document.Connections
            .Where(connection =>
                selected.Contains(connection.SourceNodeId) != selected.Contains(connection.TargetNodeId))
            .ToList();

        var replacementEdges = new List<ConnectionDocument>();

        foreach (var crossing in crossingEdges)
        {
            var sourceInside = selected.Contains(crossing.SourceNodeId);

            if (sourceInside)
            {
                // 内部 -> 外部：生成 Outlet。
                var outletId = $"out-{++boundaryCounter}";
                while (!existingIds.Add(outletId))
                {
                    outletId = $"out-{++boundaryCounter}";
                }

                var portType = FindPortType(document, registry, crossing.SourceNodeId, crossing.SourcePortId, isInput: false);
                inner.Nodes.Add(new NodeDocument
                {
                    NodeId = outletId,
                    NodeTypeId = ContainerTypeIds.BoundaryOut,
                    Ports =
                    [
                        new PortDocument { PortId = "in", TypeId = portType }
                    ]
                });
                inner.Connections.Add(new ConnectionDocument
                {
                    SourceNodeId = crossing.SourceNodeId,
                    SourcePortId = crossing.SourcePortId,
                    TargetNodeId = outletId,
                    TargetPortId = "in"
                });
                replacementEdges.Add(new ConnectionDocument
                {
                    SourceNodeId = metaNodeId,
                    SourcePortId = outletId,
                    TargetNodeId = crossing.TargetNodeId,
                    TargetPortId = crossing.TargetPortId
                });
            }
            else
            {
                // 外部 -> 内部：生成 Inlet。
                var inletId = $"in-{++boundaryCounter}";
                while (!existingIds.Add(inletId))
                {
                    inletId = $"in-{++boundaryCounter}";
                }

                var portType = FindPortType(document, registry, crossing.SourceNodeId, crossing.SourcePortId, isInput: false);
                inner.Nodes.Add(new NodeDocument
                {
                    NodeId = inletId,
                    NodeTypeId = ContainerTypeIds.BoundaryIn,
                    Ports =
                    [
                        new PortDocument { PortId = "out", TypeId = portType }
                    ]
                });
                inner.Connections.Add(new ConnectionDocument
                {
                    SourceNodeId = inletId,
                    SourcePortId = "out",
                    TargetNodeId = crossing.TargetNodeId,
                    TargetPortId = crossing.TargetPortId
                });
                replacementEdges.Add(new ConnectionDocument
                {
                    SourceNodeId = crossing.SourceNodeId,
                    SourcePortId = crossing.SourcePortId,
                    TargetNodeId = metaNodeId,
                    TargetPortId = inletId
                });
            }
        }

        foreach (var node in selectedNodes)
        {
            document.Nodes.Remove(node);
        }

        document.Connections.RemoveAll(connection =>
            selected.Contains(connection.SourceNodeId) || selected.Contains(connection.TargetNodeId));

        foreach (var connection in replacementEdges)
        {
            document.Connections.Add(connection);
        }

        var meta = new NodeDocument
        {
            NodeId = metaNodeId,
            NodeTypeId = ContainerTypeIds.MetaNode,
            X = selectedNodes.Min(node => node.X),
            Y = selectedNodes.Min(node => node.Y),
            InnerWorkflow = inner
        };

        document.Nodes.Add(meta);
        return meta;
    }

    private static string FindPortType(
        WorkflowDocument document,
        WorkflowDefinitionRegistry registry,
        string nodeId,
        string portId,
        bool isInput)
    {
        var node = document.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        if (node is null)
        {
            throw new InvalidOperationException($"找不到节点 '{nodeId}'。");
        }

        if (string.Equals(node.NodeTypeId, ContainerTypeIds.MetaNode, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("暂不支持打包涉及元节点边界的连接。");
        }

        var definition = registry.TryResolve(node.NodeTypeId)
            ?? throw new InvalidOperationException($"节点类型 '{node.NodeTypeId}' 未注册。");

        var ports = isInput ? definition.InputPorts : definition.OutputPorts;
        var port = ports.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, portId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"找不到端口 '{nodeId}.{portId}'。");

        return port.TypeId;
    }

    private static ConnectionDocument CloneConnection(ConnectionDocument connection)
    {
        return new ConnectionDocument
        {
            SourceNodeId = connection.SourceNodeId,
            SourcePortId = connection.SourcePortId,
            TargetNodeId = connection.TargetNodeId,
            TargetPortId = connection.TargetPortId
        };
    }
}
