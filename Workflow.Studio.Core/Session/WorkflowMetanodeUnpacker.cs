using System;
using System.Collections.Generic;
using System.Linq;
using Workflow.Studio.Core.Documents;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// 拆开元节点（WorkflowMetanodePacker 的逆操作）：
/// 把内部普通节点还原到外层，边界伪节点与内外跨边界连线改写回直接连接。
/// </summary>
public static class WorkflowMetanodeUnpacker
{
    public static bool Unpack(WorkflowDocument document, string metaNodeId)
    {
        ArgumentNullException.ThrowIfNull(document);

        var meta = document.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, metaNodeId, StringComparison.OrdinalIgnoreCase)
            && node.InnerWorkflow is not null);

        if (meta is null)
        {
            return false;
        }

        if (meta.VariableMappings.Count > 0)
        {
            throw new NotSupportedException("暂不支持拆开带变量映射的元节点，请先移除变量映射。");
        }

        var inner = meta.InnerWorkflow!;
        var boundaryNodeIds = inner.Nodes
            .Where(node => string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryIn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryOut, StringComparison.OrdinalIgnoreCase))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalNodes = inner.Nodes
            .Where(node => !boundaryNodeIds.Contains(node.NodeId))
            .ToList();
        var innerEdges = inner.Connections
            .Where(connection =>
                !boundaryNodeIds.Contains(connection.SourceNodeId)
                && !boundaryNodeIds.Contains(connection.TargetNodeId))
            .ToList();
        var boundaryEdges = inner.Connections
            .Where(connection =>
                boundaryNodeIds.Contains(connection.SourceNodeId)
                || boundaryNodeIds.Contains(connection.TargetNodeId))
            .ToList();

        var outerConnections = document.Connections
            .Where(connection =>
                string.Equals(connection.SourceNodeId, metaNodeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(connection.TargetNodeId, metaNodeId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var restoredConnections = new List<ConnectionDocument>();
        restoredConnections.AddRange(innerEdges.Select(CloneConnection));

        foreach (var connection in outerConnections)
        {
            if (string.Equals(connection.TargetNodeId, metaNodeId, StringComparison.OrdinalIgnoreCase))
            {
                // 外部 -> 元节点输入端口（端口 id = Inlet 伪节点 id）。
                var innerEdge = boundaryEdges.FirstOrDefault(edge =>
                    string.Equals(edge.SourceNodeId, connection.TargetPortId, StringComparison.OrdinalIgnoreCase));

                if (innerEdge is null)
                {
                    throw new InvalidOperationException($"找不到 Inlet '{connection.TargetPortId}' 的内部连接。");
                }

                restoredConnections.Add(new ConnectionDocument
                {
                    SourceNodeId = connection.SourceNodeId,
                    SourcePortId = connection.SourcePortId,
                    TargetNodeId = innerEdge.TargetNodeId,
                    TargetPortId = innerEdge.TargetPortId
                });
            }
            else
            {
                // 元节点输出端口 -> 外部（端口 id = Outlet 伪节点 id）。
                var innerEdge = boundaryEdges.FirstOrDefault(edge =>
                    string.Equals(edge.TargetNodeId, connection.SourcePortId, StringComparison.OrdinalIgnoreCase));

                if (innerEdge is null)
                {
                    throw new InvalidOperationException($"找不到 Outlet '{connection.SourcePortId}' 的内部连接。");
                }

                restoredConnections.Add(new ConnectionDocument
                {
                    SourceNodeId = innerEdge.SourceNodeId,
                    SourcePortId = innerEdge.SourcePortId,
                    TargetNodeId = connection.TargetNodeId,
                    TargetPortId = connection.TargetPortId
                });
            }
        }

        foreach (var node in normalNodes)
        {
            var absolute = new NodeDocument
            {
                NodeId = node.NodeId,
                NodeTypeId = node.NodeTypeId,
                X = meta.X + (node.X - 60),
                Y = meta.Y + (node.Y - 60),
                IsBreakpointEnabled = node.IsBreakpointEnabled,
                Settings = node.Settings,
                SettingsBindings = node.SettingsBindings.ToList(),
                VariableMappings = node.VariableMappings.ToList(),
                Ports = node.Ports.ToList(),
                InnerWorkflow = node.InnerWorkflow
            };

            document.Nodes.Add(absolute);
        }

        document.Nodes.Remove(meta);
        document.Connections.RemoveAll(connection =>
            string.Equals(connection.SourceNodeId, metaNodeId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.TargetNodeId, metaNodeId, StringComparison.OrdinalIgnoreCase));

        foreach (var connection in restoredConnections)
        {
            document.Connections.Add(connection);
        }

        return true;
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
