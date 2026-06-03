using System.Threading.Tasks;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Services;

public interface IWorkflowDebugController
{
    event EventHandler<WorkflowLogEventArgs>? LogEmitted;

    event EventHandler<BreakpointHitEventArgs>? BreakpointHit;

    bool IsPaused { get; }

    void StartSession();

    void CompleteSession();

    void EmitLog(WorkflowLogLevel level, string message, NodeData? node = null);

    Task PauseAtBreakpointAsync(NodeData node, CancellationToken cancellationToken);

    void Resume();
}

public sealed class WorkflowDebugController : IWorkflowDebugController
{
    private readonly object _syncRoot = new();
    private TaskCompletionSource<bool>? _resumeSignal;

    public event EventHandler<WorkflowLogEventArgs>? LogEmitted;

    public event EventHandler<BreakpointHitEventArgs>? BreakpointHit;

    public bool IsPaused
    {
        get
        {
            lock (_syncRoot)
            {
                return _resumeSignal is not null;
            }
        }
    }

    public void StartSession()
    {
        lock (_syncRoot)
        {
            _resumeSignal = null;
        }

        EmitLog(WorkflowLogLevel.Info, "已启动新的工作流调试会话。");
    }

    public void CompleteSession()
    {
        lock (_syncRoot)
        {
            _resumeSignal = null;
        }

        EmitLog(WorkflowLogLevel.Info, "工作流调试会话已结束。");
    }

    public void EmitLog(WorkflowLogLevel level, string message, NodeData? node = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        LogEmitted?.Invoke(
            this,
            new WorkflowLogEventArgs(
                new WorkflowLogEntry
                {
                    Timestamp = DateTimeOffset.Now,
                    Level = level,
                    Message = message,
                    NodeId = node?.Metadata.Id,
                    NodeName = node?.Metadata.Name
                }));
    }

    public async Task PauseAtBreakpointAsync(NodeData node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);

        TaskCompletionSource<bool> resumeSignal;

        lock (_syncRoot)
        {
            _resumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            resumeSignal = _resumeSignal;
        }

        BreakpointHit?.Invoke(this, new BreakpointHitEventArgs(node.Metadata.Id, node.Metadata.Name));
        EmitLog(WorkflowLogLevel.Warning, $"命中断点，等待继续执行: {node.Metadata.Name}", node);

        using var cancellationRegistration = cancellationToken.Register(() => resumeSignal.TrySetCanceled(cancellationToken));
        await resumeSignal.Task.ConfigureAwait(false);

        lock (_syncRoot)
        {
            if (ReferenceEquals(_resumeSignal, resumeSignal))
            {
                _resumeSignal = null;
            }
        }

        EmitLog(WorkflowLogLevel.Info, $"继续执行: {node.Metadata.Name}", node);
    }

    public void Resume()
    {
        TaskCompletionSource<bool>? resumeSignal;

        lock (_syncRoot)
        {
            resumeSignal = _resumeSignal;
            _resumeSignal = null;
        }

        resumeSignal?.TrySetResult(true);
    }
}

public sealed class WorkflowLogEventArgs : EventArgs
{
    public WorkflowLogEventArgs(WorkflowLogEntry entry)
    {
        Entry = entry;
    }

    public WorkflowLogEntry Entry { get; }
}

public sealed class BreakpointHitEventArgs : EventArgs
{
    public BreakpointHitEventArgs(string nodeId, string nodeName)
    {
        NodeId = nodeId;
        NodeName = nodeName;
    }

    public string NodeId { get; }

    public string NodeName { get; }
}
