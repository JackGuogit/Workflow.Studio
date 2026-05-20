using System.Collections.Concurrent;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Runtime;

public sealed class ExecutionContext
{
    private readonly object _syncRoot = new();
    private readonly List<NodeExecutionRecord> _history = [];
    private readonly List<NodeExecutionSnapshot> _snapshots = [];
    private readonly ConcurrentDictionary<string, object?> _globalVariables;
    private readonly ConcurrentDictionary<string, object?> _portSnapshots = new(StringComparer.OrdinalIgnoreCase);

    public ExecutionContext(IDictionary<string, object?> globalVariables)
    {
        _globalVariables = new ConcurrentDictionary<string, object?>(globalVariables, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<NodeExecutionRecord> History
    {
        get
        {
            lock (_syncRoot)
            {
                return _history.ToList();
            }
        }
    }

    public IReadOnlyDictionary<string, object?> PortSnapshots => _portSnapshots;

    public IReadOnlyList<NodeExecutionSnapshot> Snapshots
    {
        get
        {
            lock (_syncRoot)
            {
                return _snapshots.ToList();
            }
        }
    }

    public IDictionary<string, object?> GlobalVariables => _globalVariables;

    public void CapturePortValue(string nodeId, string portId, object? value)
    {
        _portSnapshots[BuildPortKey(nodeId, portId)] = value;
    }

    public void AddRecord(NodeExecutionRecord record)
    {
        lock (_syncRoot)
        {
            _history.Add(record);
        }
    }

    public void CaptureNodeSnapshot(
        string nodeId,
        NodeStatus status,
        IReadOnlyDictionary<string, object?> inputSnapshot,
        IReadOnlyDictionary<string, object?> outputSnapshot)
    {
        var globalVariables = _globalVariables.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        lock (_syncRoot)
        {
            _snapshots.Add(new NodeExecutionSnapshot
            {
                NodeId = nodeId,
                CapturedAt = DateTimeOffset.UtcNow,
                Status = status,
                InputSnapshot = new Dictionary<string, object?>(inputSnapshot, StringComparer.OrdinalIgnoreCase),
                OutputSnapshot = new Dictionary<string, object?>(outputSnapshot, StringComparer.OrdinalIgnoreCase),
                GlobalVariables = globalVariables
            });
        }
    }

    public object? GetPortSnapshot(string nodeId, string portId)
    {
        _portSnapshots.TryGetValue(BuildPortKey(nodeId, portId), out var value);
        return value;
    }

    private static string BuildPortKey(string nodeId, string portId)
    {
        return $"{nodeId}:{portId}";
    }
}

public sealed class NodeExecutionSnapshot
{
    public required string NodeId { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    public required NodeStatus Status { get; init; }

    public required IReadOnlyDictionary<string, object?> InputSnapshot { get; init; }

    public required IReadOnlyDictionary<string, object?> OutputSnapshot { get; init; }

    public required IReadOnlyDictionary<string, object?> GlobalVariables { get; init; }
}
