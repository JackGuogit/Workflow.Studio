namespace Workflow.Studio.Core.Models;

public enum WorkflowLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public sealed class WorkflowLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required WorkflowLogLevel Level { get; init; }

    public required string Message { get; init; }

    public string? NodeId { get; init; }

    public string? NodeName { get; init; }
}
