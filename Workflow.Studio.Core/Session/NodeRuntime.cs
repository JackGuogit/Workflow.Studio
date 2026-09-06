using System;
using System.Collections.Generic;
using System.Linq;
using Workflow.Studio.Core.Documents;

namespace Workflow.Studio.Core.Session;

/// <summary>
/// V2 节点运行时：引用文档结构与类型定义，持有状态、端口槽与错误信息。
/// </summary>
public sealed class NodeRuntime
{
    private readonly Action<NodeRuntime, NodeState, NodeState> _stateChanged;
    private readonly Dictionary<string, PortSlot> _inputs;
    private readonly Dictionary<string, PortSlot> _outputs;
    private Dictionary<string, object?>? _producedFlowVariables;

    internal NodeRuntime(
        NodeDocument sourceDocument,
        INodeDefinition definition,
        string path,
        Action<NodeRuntime, NodeState, NodeState> stateChanged)
    {
        SourceDocument = sourceDocument;
        Definition = definition;
        NodeId = sourceDocument.NodeId;
        NodeTypeId = sourceDocument.NodeTypeId;
        Path = path;
        _stateChanged = stateChanged;

        _inputs = BuildPortSlots(definition.InputPorts, "input");
        _outputs = BuildPortSlots(definition.OutputPorts, "output");

        InputPorts = definition.InputPorts
            .Select(port => _inputs[port.Id])
            .ToList();
        OutputPorts = definition.OutputPorts
            .Select(port => _outputs[port.Id])
            .ToList();
    }

    public NodeDocument SourceDocument { get; }

    public INodeDefinition Definition { get; }

    public string NodeId { get; }

    public string NodeTypeId { get; }

    /// <summary>当前容器内路径（V2 决策 R6，M5 扩展为容器链）。</summary>
    public string Path { get; }

    public NodeState State { get; private set; } = NodeState.NotConfigured;

    public string? LastError { get; private set; }

    public IReadOnlyList<PortSlot> InputPorts { get; }

    public IReadOnlyList<PortSlot> OutputPorts { get; }

    /// <summary>该节点上次成功执行产出的流变量（执行后按静态声明校验）。</summary>
    public IReadOnlyDictionary<string, object?>? ProducedFlowVariables => _producedFlowVariables;

    public PortSlot? FindInput(string portId)
    {
        return _inputs.TryGetValue(portId, out var slot) ? slot : null;
    }

    public PortSlot? FindOutput(string portId)
    {
        return _outputs.TryGetValue(portId, out var slot) ? slot : null;
    }

    /// <summary>
    /// 输出门控读取：仅 Succeeded 节点的输出可见（V2 架构文档 5.2 节）。
    /// </summary>
    public bool TryReadOutputValue(string portId, out object? value)
    {
        value = null;

        if (State != NodeState.Succeeded)
        {
            return false;
        }

        var slot = FindOutput(portId);
        if (slot is null || !slot.HasValue)
        {
            return false;
        }

        value = slot.GetValue();
        return true;
    }

    internal void SetState(NodeState state)
    {
        if (State == state)
        {
            return;
        }

        var previous = State;
        State = state;

        if (state == NodeState.NotConfigured)
        {
            ClearSpecsAndValues();
            LastError = null;
        }
        else if (state is NodeState.Failed or NodeState.Blocked)
        {
            ClearValues();
        }

        _stateChanged(this, previous, state);
    }

    internal void SetLastError(string? error)
    {
        LastError = error;
    }

    internal bool TryPublishOutputValue(string portId, object? value)
    {
        if (State != NodeState.Succeeded)
        {
            return false;
        }

        var slot = FindOutput(portId);
        if (slot is null)
        {
            return false;
        }

        slot.SetValue(value);
        return true;
    }

    internal void SetProducedFlowVariables(IReadOnlyDictionary<string, object?> variables)
    {
        _producedFlowVariables = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase);
    }

    internal void ClearProducedFlowVariables()
    {
        _producedFlowVariables = null;
    }

    private void ClearSpecsAndValues()
    {
        foreach (var slot in _inputs.Values.Concat(_outputs.Values))
        {
            slot.ClearSpec();
            slot.ClearValue();
        }

        ClearProducedFlowVariables();
    }

    private void ClearValues()
    {
        foreach (var slot in _inputs.Values.Concat(_outputs.Values))
        {
            slot.ClearValue();
        }
    }

    private static Dictionary<string, PortSlot> BuildPortSlots(
        IReadOnlyList<NodePortDefinition> definitions,
        string direction)
    {
        var slots = new Dictionary<string, PortSlot>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!slots.TryAdd(definition.Id, new PortSlot(definition)))
            {
                throw new InvalidOperationException($"Node definition declares duplicate {direction} port id '{definition.Id}'.");
            }
        }

        return slots;
    }
}
