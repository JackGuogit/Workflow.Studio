using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Services;

public interface IWorkflowConnectionValidator
{
    ConnectionValidationResult ValidateConnection(
        WorkflowData workflow,
        NodeData sourceNode,
        PortData sourcePort,
        NodeData targetNode,
        PortData targetPort,
        ConnectionData? ignoredConnection = null);

    ConnectionValidationResult ValidateConnection(
        WorkflowData workflow,
        ConnectionData connection,
        ConnectionData? ignoredConnection = null);

    void EnsureWorkflowIsValid(WorkflowData workflow);
}

public sealed class WorkflowConnectionValidator : IWorkflowConnectionValidator
{
    public ConnectionValidationResult ValidateConnection(
        WorkflowData workflow,
        NodeData sourceNode,
        PortData sourcePort,
        NodeData targetNode,
        PortData targetPort,
        ConnectionData? ignoredConnection = null)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(sourceNode);
        ArgumentNullException.ThrowIfNull(sourcePort);
        ArgumentNullException.ThrowIfNull(targetNode);
        ArgumentNullException.ThrowIfNull(targetPort);

        if (ReferenceEquals(sourcePort, targetPort))
        {
            return ConnectionValidationResult.Failure("同一个端口不能同时作为连线的起点和终点。");
        }

        if (string.Equals(sourceNode.Metadata.Id, targetNode.Metadata.Id, StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionValidationResult.Failure("暂不支持节点连接到自身。");
        }

        if (sourcePort.Direction != PortDirection.Output || targetPort.Direction != PortDirection.Input)
        {
            return ConnectionValidationResult.Failure("仅支持输出端口连接输入端口。");
        }

        if (!PortTypeCompatibility.AreCompatible(sourcePort.Metadata, targetPort.Metadata))
        {
            return ConnectionValidationResult.Failure(
                $"端口类型不兼容：{DescribePort(sourceNode, sourcePort)} 输出 '{DescribeType(sourcePort.Metadata)}'，" +
                $"{DescribePort(targetNode, targetPort)} 需要 '{DescribeType(targetPort.Metadata)}'。");
        }

        var effectiveConnections = workflow.Connections.Where(connection => !ReferenceEquals(connection, ignoredConnection)).ToList();

        if (effectiveConnections.Any(connection =>
                string.Equals(connection.SourceNodeId, sourceNode.Metadata.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(connection.SourcePortId, sourcePort.Metadata.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(connection.TargetNodeId, targetNode.Metadata.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(connection.TargetPortId, targetPort.Metadata.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return ConnectionValidationResult.Failure("相同的连线已存在，无需重复连接。");
        }

        if (effectiveConnections.Any(connection =>
                string.Equals(connection.TargetNodeId, targetNode.Metadata.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(connection.TargetPortId, targetPort.Metadata.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return ConnectionValidationResult.Failure($"输入端口仅允许保留一条入线：{DescribePort(targetNode, targetPort)}。");
        }

        return ConnectionValidationResult.Success();
    }

    public ConnectionValidationResult ValidateConnection(
        WorkflowData workflow,
        ConnectionData connection,
        ConnectionData? ignoredConnection = null)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(connection);

        var sourceNode = workflow.Nodes.FirstOrDefault(node => string.Equals(node.Metadata.Id, connection.SourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (sourceNode is null)
        {
            return ConnectionValidationResult.Failure($"无法找到源节点 '{connection.SourceNodeId}'。");
        }

        var targetNode = workflow.Nodes.FirstOrDefault(node => string.Equals(node.Metadata.Id, connection.TargetNodeId, StringComparison.OrdinalIgnoreCase));
        if (targetNode is null)
        {
            return ConnectionValidationResult.Failure($"无法找到目标节点 '{connection.TargetNodeId}'。");
        }

        var sourcePort = sourceNode.FindPort(connection.SourcePortId);
        if (sourcePort is null)
        {
            return ConnectionValidationResult.Failure($"无法找到源端口 '{connection.SourceNodeId}.{connection.SourcePortId}'。");
        }

        var targetPort = targetNode.FindPort(connection.TargetPortId);
        if (targetPort is null)
        {
            return ConnectionValidationResult.Failure($"无法找到目标端口 '{connection.TargetNodeId}.{connection.TargetPortId}'。");
        }

        return ValidateConnection(workflow, sourceNode, sourcePort, targetNode, targetPort, ignoredConnection);
    }

    public void EnsureWorkflowIsValid(WorkflowData workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        EnsureUniqueNodeIds(workflow);

        foreach (var connection in workflow.Connections)
        {
            var validationResult = ValidateConnection(workflow, connection, connection);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(validationResult.Message);
            }
        }
    }

    private static void EnsureUniqueNodeIds(WorkflowData workflow)
    {
        var duplicatedNode = workflow.Nodes
            .GroupBy(node => node.Metadata.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatedNode is not null)
        {
            throw new InvalidOperationException($"检测到重复节点标识: '{duplicatedNode.Key}'。");
        }
    }

    private static string DescribePort(NodeData node, PortData port)
    {
        return $"{node.Metadata.Name}.{port.Metadata.Name}";
    }

    private static string DescribeType(PortMetadata metadata)
    {
        var dataTypeName = PortTypeCompatibility.GetDisplayName(metadata.DataType);
        var semanticTypeName = PortTypeCompatibility.GetSemanticDisplayName(metadata.SemanticTypeKey);
        return $"{dataTypeName} / {semanticTypeName}";
    }
}

public sealed class ConnectionValidationResult
{
    private ConnectionValidationResult(bool isValid, string message)
    {
        IsValid = isValid;
        Message = message;
    }

    public bool IsValid { get; }

    public string Message { get; }

    public static ConnectionValidationResult Success()
    {
        return new ConnectionValidationResult(true, string.Empty);
    }

    public static ConnectionValidationResult Failure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ConnectionValidationResult(false, message);
    }
}
