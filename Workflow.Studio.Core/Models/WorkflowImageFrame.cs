namespace Workflow.Studio.Core.Models;

public sealed class WorkflowImageFrame
{
    public byte[] Content { get; init; } = [];

    public string Format { get; init; } = ".png";

    public int Width { get; init; }

    public int Height { get; init; }

    public int Channels { get; init; }

    public bool IsGrayscale { get; init; }

    public string? SourcePath { get; init; }
}
