using System;
using System.Collections.Generic;
using System.Linq;
using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Documents;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// V2 工作流会话：由 Document 创建运行时视图，负责 Configure 级联、
/// 脏状态传播与状态事件（V2 架构文档 5/6 节，M2 骨架）。
/// 执行调度（就绪队列/失败/Blocked）由 M3 接入；元节点容器由 M5 接入。
/// </summary>
public sealed class WorkflowSession
{
    private readonly Func<string, INodeDefinition> _definitionResolver;
    private readonly List<NodeRuntime> _nodes = [];
    private readonly Dictionary<string, NodeRuntime> _nodesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string TargetNodeId, string TargetPortId)>> _outgoing =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _declaredVariableValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _externalInputSpecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _externalInputValues = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>key = targetNodeId|targetPortId（忽略大小写）。</summary>
    private readonly Dictionary<string, (string SourceNodeId, string SourcePortId)> _incomingByTargetPort =
        new(StringComparer.OrdinalIgnoreCase);

    public WorkflowSession(
        WorkflowDocument document,
        Func<string, INodeDefinition> definitionResolver,
        ValueTypeRegistry? valueTypes = null,
        string? pathPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(definitionResolver);

        WorkflowDocumentValidator.EnsureValid(document);

        Document = document;
        _definitionResolver = definitionResolver;
        ValueTypes = valueTypes ?? ValueTypeRegistry.CreateDefault();
        PathPrefix = pathPrefix ?? string.Empty;

        BuildNodeRuntimes(document);
        BuildConnectionMaps(document);

        foreach (var declaration in document.VariableDeclarations)
        {
            _declaredVariableValues[declaration.Name] = declaration.DefaultValue;
        }
    }

    public WorkflowDocument Document { get; }

    public ValueTypeRegistry ValueTypes { get; }

    /// <summary>容器路径前缀（根为空；子容器为外层元节点路径，V2 决策 R6）。</summary>
    public string PathPrefix { get; }

    public IReadOnlyList<NodeRuntime> Nodes => _nodes;

    /// <summary>根容器（入口变量）的声明变量值。M5 扩展为按容器解析。</summary>
    public IReadOnlyDictionary<string, object?> DeclaredVariableValues => _declaredVariableValues;

    public event EventHandler<NodeStateChangedEventArgs>? NodeStateChanged;

    public NodeRuntime GetNode(string nodeId)
    {
        if (_nodesById.TryGetValue(nodeId, out var runtime))
        {
            return runtime;
        }

        throw new KeyNotFoundException($"Node '{nodeId}' was not found in the session.");
    }

    /// <summary>
    /// 编辑入口：节点结构/设置/变量变更后调用，把该节点及其传递下游置为
    /// NotConfigured 并自动重配（V2 决策 D14/R10）。
    /// </summary>
    public void NotifyNodeChanged(string nodeId)
    {
        var changed = GetNode(nodeId);
        InvalidateFrom(changed);
        ConfigureAll();
    }

    /// <summary>
    /// 更新入口变量值：做类型检查，失效所有引用该变量的节点及其下游后自动重配
    /// （V2 决策 R2/D14）。
    /// </summary>
    public void SetEntryVariable(string name, object? value)
    {
        var declaration = Document.VariableDeclarations.FirstOrDefault(
            candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Entry variable '{name}' is not declared.");

        var valueType = ValueTypes.Get(declaration.TypeId);
        if (value is not null && !valueType.PayloadType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException(
                $"Entry variable '{name}' expects type '{valueType.DisplayName}', but got '{value.GetType().Name}'.");
        }

        _declaredVariableValues[name] = value;

        foreach (var dependent in _nodes.Where(node =>
                     node.SourceDocument.SettingsBindings.Any(binding =>
                         string.Equals(binding.Variable, name, StringComparison.OrdinalIgnoreCase))))
        {
            InvalidateFrom(dependent);
        }

        ConfigureAll();
    }

    internal bool TryResolveSettings(NodeRuntime node, out IReadOnlyDictionary<string, object?> settings, out string? error)
    {
        var resolved = new Dictionary<string, object?>(node.SourceDocument.Settings, StringComparer.OrdinalIgnoreCase);

        foreach (var binding in node.SourceDocument.SettingsBindings)
        {
            if (!_declaredVariableValues.TryGetValue(binding.Variable, out var value))
            {
                settings = new Dictionary<string, object?>();
                error = $"设置字段 '{binding.Setting}' 绑定的容器变量 '{binding.Variable}' 不存在。";
                return false;
            }

            resolved[binding.Setting] = value;
        }

        settings = resolved;
        error = null;
        return true;
    }

    internal bool TryResolveVisibleFlowVariables(NodeRuntime node, out IReadOnlyDictionary<string, object?> variables, out string? error)
    {
        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var producerByVariable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var predecessor in GetTransitivePredecessors(node))
        {
            if (predecessor.ProducedFlowVariables is null)
            {
                continue;
            }

            foreach (var variable in predecessor.ProducedFlowVariables)
            {
                if (producerByVariable.TryGetValue(variable.Key, out var existingProducer)
                    && !string.Equals(existingProducer, predecessor.NodeId, StringComparison.OrdinalIgnoreCase))
                {
                    variables = new Dictionary<string, object?>();
                    error = $"流变量 '{variable.Key}' 同时来自多个前驱，无法确定取值。";
                    return false;
                }

                producerByVariable[variable.Key] = predecessor.NodeId;
                merged[variable.Key] = variable.Value;
            }
        }

        variables = merged;
        error = null;
        return true;
    }

    internal void SetExternalInputSpec(string nodeId, string portId, object? spec)
    {
        _externalInputSpecs[BuildTargetKey(nodeId, portId)] = spec;
    }

    internal void SetExternalInputValue(string nodeId, string portId, object? value)
    {
        _externalInputValues[BuildTargetKey(nodeId, portId)] = value;
    }

    internal bool TryGetExternalInputValue(string nodeId, string portId, out object? value)
    {
        return _externalInputValues.TryGetValue(BuildTargetKey(nodeId, portId), out value);
    }

    internal void SetDeclaredVariableValue(string name, object? value)
    {
        var declaration = Document.VariableDeclarations.FirstOrDefault(
            candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Variable '{name}' is not declared in this container.");

        var valueType = ValueTypes.Get(declaration.TypeId);
        if (value is not null && !valueType.PayloadType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException(
                $"Variable '{name}' expects type '{valueType.DisplayName}', but got '{value.GetType().Name}'.");
        }

        _declaredVariableValues[name] = value;
    }

    /// <summary>按拓扑顺序配置全部脏节点；上游干净（Configured）节点复用。</summary>
    public void ConfigureAll()
    {
        var ordered = TopologicalOrder();

        foreach (var node in ordered)
        {
            if (node.State == NodeState.NotConfigured)
            {
                TryConfigureNode(node);
            }
        }
    }

    /// <summary>
    /// 状态机入口（由 M3 调度器/执行器使用；编辑器不得直接设置执行态）。
    /// 仅允许设置执行类状态。
    /// </summary>
    public void SetNodeState(string nodeId, NodeState state)
    {
        if (state is NodeState.NotConfigured or NodeState.Configured)
        {
            throw new InvalidOperationException(
                $"State '{state}' is managed by the configuration engine; use edit/configure APIs instead.");
        }

        GetNode(nodeId).SetState(state);
    }

    /// <summary>
    /// 发布输出值（M3 执行器调用）。输出门控：仅 Succeeded 节点可发布。
    /// </summary>
    public void PublishOutputValue(string nodeId, string portId, object? value)
    {
        var node = GetNode(nodeId);
        if (!node.TryPublishOutputValue(portId, value))
        {
            throw new InvalidOperationException(
                $"Cannot publish output '{node.Path}.{portId}': node must be in state {NodeState.Succeeded} and the port must exist.");
        }
    }

    internal bool TryGetIncomingSource(string nodeId, string portId, out NodeRuntime? sourceNode, out string sourcePortId)
    {
        var key = BuildTargetKey(nodeId, portId);
        if (_incomingByTargetPort.TryGetValue(key, out var incoming))
        {
            sourceNode = _nodesById[incoming.SourceNodeId];
            sourcePortId = incoming.SourcePortId;
            return true;
        }

        sourceNode = null;
        sourcePortId = string.Empty;
        return false;
    }

    internal IReadOnlyList<(string TargetNodeId, string TargetPortId)> GetOutgoingConnections(string nodeId)
    {
        return _outgoing.TryGetValue(nodeId, out var connections) ? connections : [];
    }

    private void BuildNodeRuntimes(WorkflowDocument document)
    {
        foreach (var nodeDocument in document.Nodes)
        {
            var path = $"{PathPrefix}/{nodeDocument.NodeId}";
            var definition = ResolveDefinition(nodeDocument);
            var runtime = new NodeRuntime(nodeDocument, definition, path, OnNodeStateChanged);

            _nodes.Add(runtime);
            _nodesById.Add(runtime.NodeId, runtime);
            _outgoing[runtime.NodeId] = [];
        }
    }

    private INodeDefinition ResolveDefinition(NodeDocument nodeDocument)
    {
        if (string.Equals(nodeDocument.NodeTypeId, ContainerTypeIds.MetaNode, StringComparison.OrdinalIgnoreCase))
        {
            if (nodeDocument.InnerWorkflow is null)
            {
                throw new InvalidOperationException(
                    $"Metanode '{nodeDocument.NodeId}' must carry an InnerWorkflow.");
            }

            return new ContainerNodeDefinition(nodeDocument, _definitionResolver, ValueTypes);
        }

        if (string.Equals(nodeDocument.NodeTypeId, ContainerTypeIds.BoundaryIn, StringComparison.OrdinalIgnoreCase)
            || string.Equals(nodeDocument.NodeTypeId, ContainerTypeIds.BoundaryOut, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Boundary pseudo node '{nodeDocument.NodeId}' can only appear inside a metanode's inner workflow.");
        }

        INodeDefinition? definition;

        try
        {
            definition = _definitionResolver(nodeDocument.NodeTypeId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to resolve definition for node type '{nodeDocument.NodeTypeId}' (node '{nodeDocument.NodeId}'): {ex.Message}",
                ex);
        }

        if (definition is null)
        {
            throw new InvalidOperationException(
                $"No definition registered for node type '{nodeDocument.NodeTypeId}' (node '{nodeDocument.NodeId}').");
        }

        return definition;
    }

    private void BuildConnectionMaps(WorkflowDocument document)
    {
        foreach (var connection in document.Connections)
        {
            var source = _nodesById[connection.SourceNodeId];
            var target = _nodesById[connection.TargetNodeId];

            if (source.FindOutput(connection.SourcePortId) is null)
            {
                throw new InvalidOperationException(
                    $"Connection references unknown output port '{source.Path}.{connection.SourcePortId}'.");
            }

            if (target.FindInput(connection.TargetPortId) is null)
            {
                throw new InvalidOperationException(
                    $"Connection references unknown input port '{target.Path}.{connection.TargetPortId}'.");
            }

            var sourceTypeId = source.FindOutput(connection.SourcePortId)!.TypeId;
            var targetTypeId = target.FindInput(connection.TargetPortId)!.TypeId;
            if (!ValueTypes.AreCompatible(sourceTypeId, targetTypeId))
            {
                throw new InvalidOperationException(
                    $"Connection type mismatch: '{source.Path}.{connection.SourcePortId}' ({sourceTypeId}) -> " +
                    $"'{target.Path}.{connection.TargetPortId}' ({targetTypeId}).");
            }

            var targetKey = BuildTargetKey(connection.TargetNodeId, connection.TargetPortId);
            if (!_incomingByTargetPort.TryAdd(targetKey, (connection.SourceNodeId, connection.SourcePortId)))
            {
                throw new InvalidOperationException(
                    $"Input port '{target.Path}.{connection.TargetPortId}' already has an incoming connection.");
            }

            _outgoing[connection.SourceNodeId].Add((connection.TargetNodeId, connection.TargetPortId));
        }
    }

    private void InvalidateFrom(NodeRuntime start)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<NodeRuntime>();
        queue.Enqueue(start);
        visited.Add(start.NodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            current.SetState(NodeState.NotConfigured);

            foreach (var (targetNodeId, _) in _outgoing[current.NodeId])
            {
                if (visited.Add(targetNodeId))
                {
                    queue.Enqueue(_nodesById[targetNodeId]);
                }
            }
        }
    }

    private bool TryConfigureNode(NodeRuntime node)
    {
        var visibleConflict = FindVisibleFlowVariableConflict(node);
        if (visibleConflict is not null)
        {
            node.SetState(NodeState.NotConfigured);
            node.SetLastError(visibleConflict);
            return false;
        }

        if (!TryResolveSettings(node, out var resolvedSettings, out var settingsError))
        {
            node.SetState(NodeState.NotConfigured);
            node.SetLastError(settingsError);
            return false;
        }

        var inputSpecs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var missingInput = false;

        foreach (var inputPort in node.InputPorts)
        {
            var key = BuildTargetKey(node.NodeId, inputPort.PortId);

            if (_incomingByTargetPort.TryGetValue(key, out var incoming))
            {
                var sourceNode = _nodesById[incoming.SourceNodeId];
                var sourceSlot = sourceNode.FindOutput(incoming.SourcePortId)!;

                if (sourceSlot.IsSpecComputed)
                {
                    inputSpecs[inputPort.PortId] = sourceSlot.Spec;
                }
                else
                {
                    missingInput = true;
                }
            }
            else if (_externalInputSpecs.TryGetValue(key, out var externalSpec))
            {
                inputSpecs[inputPort.PortId] = externalSpec;
            }
            else if (!inputPort.IsOptional)
            {
                missingInput = true;
            }
        }

        if (missingInput)
        {
            node.SetState(NodeState.NotConfigured);
            node.SetLastError($"节点缺少必连输入配置（无上游或上游尚未配置）。");
            return false;
        }

        NodeConfigureResult result;

        try
        {
            result = node.Definition.Configure(new NodeConfigureRequest
            {
                SourceDocument = node.SourceDocument,
                NodePath = node.Path,
                InputSpecs = inputSpecs,
                Settings = resolvedSettings,
                DeclaredVariables = _declaredVariableValues
            });
        }
        catch (Exception ex)
        {
            node.SetState(NodeState.NotConfigured);
            node.SetLastError($"Configure 抛异常: {ex.Message}");
            return false;
        }

        ArgumentNullException.ThrowIfNull(result);

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            node.SetState(NodeState.NotConfigured);
            node.SetLastError(result.Error);
            return false;
        }

        var missingOutputSpecs = node.OutputPorts
            .Where(port => !result.OutputSpecs.ContainsKey(port.PortId))
            .Select(port => port.PortId)
            .ToList();

        if (missingOutputSpecs.Count > 0)
        {
            node.SetState(NodeState.NotConfigured);
            node.SetLastError($"Configure 未提供输出 Spec: {string.Join(", ", missingOutputSpecs)}。");
            return false;
        }

        foreach (var inputPort in node.InputPorts)
        {
            if (inputSpecs.TryGetValue(inputPort.PortId, out var spec))
            {
                inputPort.SetSpec(spec);
            }
        }

        foreach (var outputPort in node.OutputPorts)
        {
            outputPort.SetSpec(result.OutputSpecs[outputPort.PortId]);
        }

        node.SetLastError(null);
        node.SetState(NodeState.Configured);
        return true;
    }

    /// <summary>
    /// 静态可见性冲突检查：流变量名允许在不同分支复用；
    /// 只有当同一名字会同时被某个下游节点看到（多个前驱产出同名）时才是配置错误。
    /// </summary>
    private string? FindVisibleFlowVariableConflict(NodeRuntime node)
    {
        var producerByVariable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var predecessor in GetTransitivePredecessors(node))
        {
            foreach (var declaration in predecessor.Definition.OutputVariables)
            {
                if (producerByVariable.TryGetValue(declaration.Name, out var existingProducer)
                    && !string.Equals(existingProducer, predecessor.NodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return $"检测到同名流变量 '{declaration.Name}' 同时来自多个前驱，下游节点 '{node.NodeId}' 无法确定取值。";
                }

                producerByVariable[declaration.Name] = predecessor.NodeId;
            }
        }

        return null;
    }

    private IReadOnlyList<NodeRuntime> GetTransitivePredecessors(NodeRuntime node)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var predecessors = new List<NodeRuntime>();
        var queue = new Queue<NodeRuntime>();

        foreach (var inputPort in node.InputPorts)
        {
            if (TryGetIncomingSource(node.NodeId, inputPort.PortId, out var source, out _) && source is not null)
            {
                queue.Enqueue(source);
                visited.Add(source.NodeId);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            predecessors.Add(current);

            foreach (var inputPort in current.InputPorts)
            {
                if (TryGetIncomingSource(current.NodeId, inputPort.PortId, out var source, out _)
                    && source is not null
                    && visited.Add(source.NodeId))
                {
                    queue.Enqueue(source);
                }
            }
        }

        return predecessors;
    }

    private IReadOnlyList<NodeRuntime> TopologicalOrder()
    {
        var indegree = _nodes.ToDictionary(node => node.NodeId, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var connection in Document.Connections)
        {
            indegree[connection.TargetNodeId]++;
        }

        var queue = new Queue<NodeRuntime>(
            _nodes
                .Where(node => indegree[node.NodeId] == 0)
                .OrderBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase));
        var ordered = new List<NodeRuntime>(_nodes.Count);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var (targetNodeId, _) in _outgoing[current.NodeId])
            {
                indegree[targetNodeId]--;
                if (indegree[targetNodeId] == 0)
                {
                    queue.Enqueue(_nodesById[targetNodeId]);
                }
            }
        }

        if (ordered.Count != _nodes.Count)
        {
            throw new InvalidOperationException("Workflow contains a cycle and cannot be configured as a DAG.");
        }

        return ordered;
    }

    private void OnNodeStateChanged(NodeRuntime node, NodeState previous, NodeState current)
    {
        NodeStateChanged?.Invoke(
            this,
            new NodeStateChangedEventArgs(node.NodeId, node.Path, previous, current));
    }

    private static string BuildTargetKey(string nodeId, string portId)
    {
        return $"{nodeId}|{portId}";
    }
}

public sealed class NodeStateChangedEventArgs : EventArgs
{
    public NodeStateChangedEventArgs(string nodeId, string nodePath, NodeState previousState, NodeState newState)
    {
        NodeId = nodeId;
        NodePath = nodePath;
        PreviousState = previousState;
        NewState = newState;
    }

    public string NodeId { get; }

    public string NodePath { get; }

    public NodeState PreviousState { get; }

    public NodeState NewState { get; }
}
