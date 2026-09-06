using System;
using System.Collections.Generic;
using System.Linq;

namespace Workflow.Studio.Core.Documents;

/// <summary>
/// Document v2 的结构校验（M1 范围）：
/// 版本、容器内节点 Id 唯一、变量声明唯一、边界伪节点端口形状、
/// 连接端点存在性、映射/绑定字段完整性。
/// 端口类型兼容与节点端口结构校验在接入 Catalog/节点契约后（M2）执行。
/// </summary>
public static class WorkflowDocumentValidator
{
    public static IReadOnlyList<string> Validate(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();

        if (document.SchemaVersion != WorkflowDocument.CurrentSchemaVersion)
        {
            errors.Add($"Unsupported schema version '{document.SchemaVersion}' (expected {WorkflowDocument.CurrentSchemaVersion}).");
        }

        ValidateContainer(document, "<root>", errors);
        return errors;
    }

    public static void EnsureValid(WorkflowDocument document)
    {
        var errors = Validate(document);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Workflow document is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateContainer(WorkflowDocument document, string containerPath, List<string> errors)
    {
        ValidateVariableDeclarations(document, containerPath, errors);

        var duplicateNodeId = document.Nodes
            .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateNodeId is not null)
        {
            errors.Add($"{containerPath}: duplicate node id '{duplicateNodeId.Key}'.");
        }

        var nodesById = document.Nodes
            .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var node in document.Nodes)
        {
            ValidateNode(node, containerPath, errors);
        }

        foreach (var connection in document.Connections)
        {
            ValidateConnection(connection, containerPath, nodesById, errors);
        }
    }

    private static void ValidateVariableDeclarations(WorkflowDocument document, string containerPath, List<string> errors)
    {
        foreach (var declaration in document.VariableDeclarations)
        {
            if (string.IsNullOrWhiteSpace(declaration.Name))
            {
                errors.Add($"{containerPath}: variable declaration requires a name.");
            }

            if (string.IsNullOrWhiteSpace(declaration.TypeId))
            {
                errors.Add($"{containerPath}: variable '{declaration.Name}' requires a TypeId.");
            }
        }

        var duplicateName = document.VariableDeclarations
            .GroupBy(declaration => declaration.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateName is not null)
        {
            errors.Add($"{containerPath}: duplicate variable declaration '{duplicateName.Key}'.");
        }
    }

    private static void ValidateNode(NodeDocument node, string containerPath, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(node.NodeId))
        {
            errors.Add($"{containerPath}: node requires an id.");
            return;
        }

        var location = $"{containerPath}/{node.NodeId}";

        if (string.IsNullOrWhiteSpace(node.NodeTypeId))
        {
            errors.Add($"{location}: node requires a NodeTypeId.");
            return;
        }

        var isMetaNode = string.Equals(node.NodeTypeId, ContainerTypeIds.MetaNode, StringComparison.OrdinalIgnoreCase);
        if (isMetaNode != (node.InnerWorkflow is not null))
        {
            errors.Add($"{location}: metanode must carry an InnerWorkflow and only core.metanode may do so.");
        }

        var isBoundaryIn = string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryIn, StringComparison.OrdinalIgnoreCase);
        var isBoundaryOut = string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryOut, StringComparison.OrdinalIgnoreCase);

        if (isBoundaryIn || isBoundaryOut)
        {
            if (node.Ports.Count != 1)
            {
                errors.Add($"{location}: boundary node must declare exactly one port.");
            }
            else
            {
                var port = node.Ports[0];
                if (string.IsNullOrWhiteSpace(port.PortId) || string.IsNullOrWhiteSpace(port.TypeId))
                {
                    errors.Add($"{location}: boundary port requires PortId and TypeId.");
                }
            }

            if (node.SettingsBindings.Count > 0 || node.VariableMappings.Count > 0)
            {
                errors.Add($"{location}: boundary node must not declare settings bindings or variable mappings.");
            }
        }

        if (node.SettingsBindings.Any(binding => string.IsNullOrWhiteSpace(binding.Setting) || string.IsNullOrWhiteSpace(binding.Variable)))
        {
            errors.Add($"{location}: settings binding requires both setting and variable names.");
        }

        foreach (var mapping in node.VariableMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.Source) || string.IsNullOrWhiteSpace(mapping.Target))
            {
                errors.Add($"{location}: variable mapping requires source and target names.");
                continue;
            }

            if (mapping.Direction == VariableMappingDirection.In)
            {
                var targetExists = node.InnerWorkflow?.VariableDeclarations.Any(
                    declaration => string.Equals(declaration.Name, mapping.Target, StringComparison.OrdinalIgnoreCase)) ?? false;

                if (!targetExists)
                {
                    errors.Add($"{location}: in mapping target '{mapping.Target}' is not declared in the inner workflow.");
                }
            }
        }

        if (node.InnerWorkflow is not null)
        {
            ValidateContainer(node.InnerWorkflow, location, errors);
        }
    }

    private static void ValidateConnection(
        ConnectionDocument connection,
        string containerPath,
        IReadOnlyDictionary<string, NodeDocument> nodesById,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(connection.SourceNodeId)
            || string.IsNullOrWhiteSpace(connection.SourcePortId)
            || string.IsNullOrWhiteSpace(connection.TargetNodeId)
            || string.IsNullOrWhiteSpace(connection.TargetPortId))
        {
            errors.Add($"{containerPath}: connection endpoints must be fully specified.");
            return;
        }

        if (!nodesById.TryGetValue(connection.SourceNodeId, out var sourceNode))
        {
            errors.Add($"{containerPath}: connection source node '{connection.SourceNodeId}' does not exist.");
            return;
        }

        if (!nodesById.TryGetValue(connection.TargetNodeId, out var targetNode))
        {
            errors.Add($"{containerPath}: connection target node '{connection.TargetNodeId}' does not exist.");
            return;
        }

        ValidatePortReference(connection.SourceNodeId, connection.SourcePortId, sourceNode, containerPath, errors, isSource: true);
        ValidatePortReference(connection.TargetNodeId, connection.TargetPortId, targetNode, containerPath, errors, isSource: false);
    }

    private static void ValidatePortReference(
        string nodeId,
        string portId,
        NodeDocument node,
        string containerPath,
        List<string> errors,
        bool isSource)
    {
        // 普通节点的端口表面由节点类型契约派生（M2 接入 Catalog 后校验）；
        // 边界伪节点的端口按实例持久化，这里可直接校验。
        if (node.Ports.Count == 0)
        {
            return;
        }

        if (node.Ports.All(port => !string.Equals(port.PortId, portId, StringComparison.OrdinalIgnoreCase)))
        {
            var role = isSource ? "source" : "target";
            errors.Add($"{containerPath}: connection {role} port '{nodeId}.{portId}' is not declared on boundary node.");
        }
    }
}
