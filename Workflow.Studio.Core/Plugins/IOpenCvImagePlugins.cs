using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Plugins;

public interface IOpenCvImageCodecPlugin
{
    ValueTask<WorkflowImageFrame> LoadAsync(
        string filePath,
        WorkflowImageReadMode readMode,
        CancellationToken cancellationToken);

    ValueTask SaveAsync(
        WorkflowImageFrame image,
        string filePath,
        CancellationToken cancellationToken);
}

public interface IOpenCvImageProcessingPlugin
{
    ValueTask<WorkflowImageFrame> ConvertToGrayscaleAsync(
        WorkflowImageFrame image,
        CancellationToken cancellationToken);

    ValueTask<WorkflowImageFrame> ApplyThresholdAsync(
        WorkflowImageFrame image,
        byte threshold,
        byte maxValue,
        WorkflowThresholdMode mode,
        bool autoConvertToGrayscale,
        CancellationToken cancellationToken);
}
