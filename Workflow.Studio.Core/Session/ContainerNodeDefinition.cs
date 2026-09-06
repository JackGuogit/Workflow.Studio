using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Documents;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// 元节点的容器定义（M5）：外层把元节点当作一个普通节点入队；
/// Configure/Execute 时基于 InnerWorkflow 构造子会话，注入边界输入，
/// 执行后从边界输出源收集结果。变量 in/out 映射的数据接入为本切片的后续部分。
/// </summary>
internal sealed class ContainerNodeDefinition : INodeDefinition
{
    private readonly NodeDocument _sourceDocument;
    private readonly Func<string, INodeDefinition> _definitionResolver;
    private readonly ValueTypeRegistry _valueTypes;
    private readonly IReadOnlyList<NodePortDefinition> _inputPorts;
    private readonly IReadOnlyList<NodePortDefinition> _outputPorts;
    private readonly IReadOnlyList<VariableMapping> _inMappings;
    private readonly IReadOnlyList<VariableMapping> _outMappings;
    private readonly IReadOnlyList<FlowVariableDeclaration> _outputVariableDeclarations;

    public ContainerNodeDefinition(
        NodeDocument sourceDocument,
        Func<string, INodeDefinition> definitionResolver,
        ValueTypeRegistry valueTypes)
    {
        ArgumentNullException.ThrowIfNull(sourceDocument);
        ArgumentNullException.ThrowIfNull(definitionResolver);
        ArgumentNullException.ThrowIfNull(valueTypes);

        _sourceDocument = sourceDocument;
        _definitionResolver = definitionResolver;
        _valueTypes = valueTypes;

        var innerWorkflow = sourceDocument.InnerWorkflow
            ?? throw new InvalidOperationException($"Metanode '{sourceDocument.NodeId}' must carry an InnerWorkflow.");

        _inputPorts = innerWorkflow.Nodes
            .Where(node => string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryIn, StringComparison.OrdinalIgnoreCase))
            .OrderBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(node => ToPortDefinition(node))
            .ToList();

        _outputPorts = innerWorkflow.Nodes
            .Where(node => string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryOut, StringComparison.OrdinalIgnoreCase))
            .OrderBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(node => ToPortDefinition(node))
            .ToList();

        _inMappings = sourceDocument.VariableMappings
            .Where(mapping => mapping.Direction == VariableMappingDirection.In)
            .ToList();
        _outMappings = sourceDocument.VariableMappings
            .Where(mapping => mapping.Direction == VariableMappingDirection.Out)
            .ToList();
        _outputVariableDeclarations = ResolveOutputVariableDeclarations(innerWorkflow);
    }

    public IReadOnlyList<NodePortDefinition> InputPorts => _inputPorts;

    public IReadOnlyList<NodePortDefinition> OutputPorts => _outputPorts;

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => _outputVariableDeclarations;

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        var (childDocument, inletTargets, outletSources) = BuildChildModel(_sourceDocument.InnerWorkflow!);
        var childSession = new WorkflowSession(childDocument, _definitionResolver, _valueTypes, request.NodePath);

        foreach (var mapping in _inMappings)
        {
            if (request.DeclaredVariables.TryGetValue(mapping.Source, out var outerValue))
            {
                childSession.SetDeclaredVariableValue(mapping.Target, outerValue);
            }
        }

        foreach (var inputPort in _inputPorts)
        {
            if (!request.InputSpecs.TryGetValue(inputPort.Id, out var spec))
            {
                return Error("元节点输入端口缺少上游 Spec。");
            }

            if (inletTargets.TryGetValue(inputPort.Id, out var targets))
            {
                foreach (var (targetNodeId, targetPortId) in targets)
                {
                    childSession.SetExternalInputSpec(targetNodeId, targetPortId, spec);
                }
            }
        }

        childSession.ConfigureAll();

        var notConfigured = childSession.Nodes
            .Where(node => node.State == NodeState.NotConfigured)
            .Select(node => $"{node.NodeId}: {node.LastError ?? "未配置"}")
            .ToList();

        if (notConfigured.Count > 0)
        {
            return Error($"子工作流配置失败: {string.Join("; ", notConfigured)}");
        }

        var outputSpecs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var outputPort in _outputPorts)
        {
            if (!outletSources.TryGetValue(outputPort.Id, out var source))
            {
                return Error($"边界输出 '{outputPort.Id}' 缺少内部来源。");
            }

            var sourceNode = childSession.GetNode(source.NodeId);
            var sourceSlot = sourceNode.FindOutput(source.PortId);
            if (sourceSlot is null || !sourceSlot.IsSpecComputed)
            {
                return Error($"边界输出 '{outputPort.Id}' 的内部来源未产生 Spec。");
            }

            outputSpecs[outputPort.Id] = sourceSlot.Spec;
        }

        return new NodeConfigureResult
        {
            OutputSpecs = outputSpecs
        };
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var (childDocument, inletTargets, outletSources) = BuildChildModel(_sourceDocument.InnerWorkflow!);
        var childSession = new WorkflowSession(childDocument, _definitionResolver, _valueTypes, request.NodePath);

        foreach (var mapping in _inMappings)
        {
            if (!TryResolveOuterVariable(mapping.Source, request, out var outerValue))
            {
                return ErrorResult($"in 映射源变量 '{mapping.Source}' 不可见（既不是声明变量也不是前驱流变量）。");
            }

            childSession.SetDeclaredVariableValue(mapping.Target, outerValue);
        }

        foreach (var inputPort in _inputPorts)
        {
            if (!request.InputValues.TryGetValue(inputPort.Id, out var value))
            {
                return ErrorResult("元节点输入端口缺少输入值。");
            }

            if (inletTargets.TryGetValue(inputPort.Id, out var targets))
            {
                foreach (var (targetNodeId, targetPortId) in targets)
                {
                    childSession.SetExternalInputValue(targetNodeId, targetPortId, value);
                    // 执行期仅需让内部 Configure 通过；Spec 内容在 Configure 阶段由外层提供。
                    childSession.SetExternalInputSpec(targetNodeId, targetPortId, null);
                }
            }
        }

        childSession.ConfigureAll();

        var childExecutor = new WorkflowExecutor(childSession);
        var innerResult = await childExecutor.ExecuteAllAsync(cancellationToken);

        if (innerResult.IsCanceled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return ErrorResult("子工作流执行被取消。");
        }

        if (innerResult.HasFailures)
        {
            var failures = childSession.Nodes
                .Where(node => node.State is NodeState.Failed or NodeState.Blocked)
                .Select(node => $"{node.NodeId}: {node.LastError ?? node.State.ToString()}")
                .ToList();

            return ErrorResult($"子工作流执行失败: {string.Join("; ", failures)}");
        }

        var outputValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var outputVariables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var outputPort in _outputPorts)
        {
            if (!outletSources.TryGetValue(outputPort.Id, out var source))
            {
                return ErrorResult($"边界输出 '{outputPort.Id}' 缺少内部来源。");
            }

            var sourceNode = childSession.GetNode(source.NodeId);
            if (!sourceNode.TryReadOutputValue(source.PortId, out var value))
            {
                return ErrorResult($"边界输出 '{outputPort.Id}' 的内部来源未产生输出。");
            }

            outputValues[outputPort.Id] = value;
        }

        foreach (var mapping in _outMappings)
        {
            if (!TryReadInnerVariable(childSession, mapping.Source, out var variableValue))
            {
                return ErrorResult($"out 映射源变量 '{mapping.Source}' 在子工作流中未产出。");
            }

            outputVariables[mapping.Target] = variableValue;
        }

        return new NodeExecutionResult
        {
            OutputValues = outputValues,
            OutputVariables = outputVariables,
            Message = $"子工作流执行完成，共执行 {innerResult.ExecutedNodeIds.Count} 个内部节点。"
        };
    }

    private IReadOnlyList<FlowVariableDeclaration> ResolveOutputVariableDeclarations(WorkflowDocument innerWorkflow)
    {
        var declarations = new List<FlowVariableDeclaration>();

        foreach (var mapping in _outMappings)
        {
            var producer = innerWorkflow.Nodes.FirstOrDefault(node =>
            {
                if (string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryIn, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryOut, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(node.NodeTypeId, ContainerTypeIds.MetaNode, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"out 映射 '{mapping.Source}' 的目标位于嵌套元节点内，本切片暂不支持跨两层变量映射。");
                }

                var definition = _definitionResolver(node.NodeTypeId);
                return definition is not null
                    && definition.OutputVariables.Any(declaration =>
                        string.Equals(declaration.Name, mapping.Source, StringComparison.OrdinalIgnoreCase));
            });

            if (producer is null)
            {
                throw new InvalidOperationException(
                    $"out 映射源变量 '{mapping.Source}' 未在子工作流中找到产出节点。");
            }

            var producerDefinition = _definitionResolver(producer.NodeTypeId)!;
            var declaration = producerDefinition.OutputVariables.First(declaration =>
                string.Equals(declaration.Name, mapping.Source, StringComparison.OrdinalIgnoreCase));

            declarations.Add(new FlowVariableDeclaration(mapping.Target, declaration.TypeId));
        }

        return declarations;
    }

    private static bool TryResolveOuterVariable(
        string source,
        NodeExecutionRequest request,
        out object? value)
    {
        if (request.Variables.TryGetValue(source, out value))
        {
            return true;
        }

        return request.DeclaredVariables.TryGetValue(source, out value);
    }

    private static bool TryReadInnerVariable(WorkflowSession childSession, string source, out object? value)
    {
        foreach (var node in childSession.Nodes)
        {
            if (node.ProducedFlowVariables is { } produced
                && produced.TryGetValue(source, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static NodePortDefinition ToPortDefinition(NodeDocument boundaryNode)
    {
        var port = boundaryNode.Ports[0];
        return new NodePortDefinition(
            boundaryNode.NodeId,
            port.TypeId,
            IsOptional: false,
            DisplayName: port.PortId);
    }

    private static NodeConfigureResult Error(string message)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?>(),
            Error = message
        };
    }

    private static NodeExecutionResult ErrorResult(string message)
    {
        return new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?>(),
            Error = message
        };
    }

    private static (WorkflowDocument Model, Dictionary<string, List<(string NodeId, string PortId)>> InletTargets, Dictionary<string, (string NodeId, string PortId)> OutletSources) BuildChildModel(
        WorkflowDocument innerWorkflow)
    {
        var boundaryNodeIds = innerWorkflow.Nodes
            .Where(node => string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryIn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeTypeId, ContainerTypeIds.BoundaryOut, StringComparison.OrdinalIgnoreCase))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var model = new WorkflowDocument
        {
            SchemaVersion = WorkflowDocument.CurrentSchemaVersion,
            VariableDeclarations = innerWorkflow.VariableDeclarations.ToList(),
            Nodes = innerWorkflow.Nodes.Where(node => !boundaryNodeIds.Contains(node.NodeId)).ToList(),
            Connections = innerWorkflow.Connections
                .Where(connection =>
                    !boundaryNodeIds.Contains(connection.SourceNodeId)
                    && !boundaryNodeIds.Contains(connection.TargetNodeId))
                .Select(connection => new ConnectionDocument
                {
                    SourceNodeId = connection.SourceNodeId,
                    SourcePortId = connection.SourcePortId,
                    TargetNodeId = connection.TargetNodeId,
                    TargetPortId = connection.TargetPortId
                })
                .ToList()
        };

        var inletTargets = new Dictionary<string, List<(string NodeId, string PortId)>>(StringComparer.OrdinalIgnoreCase);
        var outletSources = new Dictionary<string, (string NodeId, string PortId)>(StringComparer.OrdinalIgnoreCase);

        foreach (var connection in innerWorkflow.Connections)
        {
            if (boundaryNodeIds.Contains(connection.SourceNodeId))
            {
                if (!inletTargets.TryGetValue(connection.SourceNodeId, out var targets))
                {
                    targets = [];
                    inletTargets[connection.SourceNodeId] = targets;
                }

                targets.Add((connection.TargetNodeId, connection.TargetPortId));
            }

            if (boundaryNodeIds.Contains(connection.TargetNodeId))
            {
                outletSources[connection.TargetNodeId] = (connection.SourceNodeId, connection.SourcePortId);
            }
        }

        return (model, inletTargets, outletSources);
    }
}
