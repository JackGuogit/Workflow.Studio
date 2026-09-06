using System.IO;
using OpenCvSharp;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Plugins;

namespace Workflow.Studio.Plugins.BuiltIn;

public sealed class OpenCvImageCodecPlugin : IOpenCvImageCodecPlugin
{
    public ValueTask<WorkflowImageFrame> LoadAsync(
        string filePath,
        WorkflowImageReadMode readMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("图片文件路径不能为空。");
        }

        var resolvedPath = Path.GetFullPath(filePath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("无法找到要读取的图片文件。", resolvedPath);
        }

        using var image = Cv2.ImRead(resolvedPath, OpenCvImageFrameConverter.MapReadMode(readMode));
        if (image.Empty())
        {
            throw new InvalidOperationException($"OpenCV 无法读取图片文件: {resolvedPath}");
        }

        return ValueTask.FromResult(OpenCvImageFrameConverter.CreateFrame(
            image,
            Path.GetExtension(resolvedPath),
            resolvedPath));
    }

    public ValueTask SaveAsync(
        WorkflowImageFrame image,
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(image);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("图片输出路径不能为空。");
        }

        var resolvedPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(resolvedPath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        using var mat = OpenCvImageFrameConverter.DecodeFrame(image);
        if (!Cv2.ImWrite(resolvedPath, mat))
        {
            throw new InvalidOperationException($"OpenCV 无法保存图片文件: {resolvedPath}");
        }

        return ValueTask.CompletedTask;
    }

}

public sealed class OpenCvImageProcessingPlugin : IOpenCvImageProcessingPlugin
{
    public ValueTask<WorkflowImageFrame> ConvertToGrayscaleAsync(
        WorkflowImageFrame image,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(image);

        using var source = OpenCvImageFrameConverter.DecodeFrame(image);
        using var result = source.Channels() switch
        {
            1 => source.Clone(),
            3 => ConvertColor(source, ColorConversionCodes.BGR2GRAY),
            4 => ConvertColor(source, ColorConversionCodes.BGRA2GRAY),
            _ => throw new InvalidOperationException($"暂不支持通道数为 {source.Channels()} 的图片灰度化。")
        };

        return ValueTask.FromResult(OpenCvImageFrameConverter.CreateFrame(
            result,
            image.Format,
            image.SourcePath));
    }

    public async ValueTask<WorkflowImageFrame> ApplyThresholdAsync(
        WorkflowImageFrame image,
        byte threshold,
        byte maxValue,
        WorkflowThresholdMode mode,
        bool autoConvertToGrayscale,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(image);

        var grayscaleImage = image;
        if (autoConvertToGrayscale)
        {
            grayscaleImage = await ConvertToGrayscaleAsync(image, cancellationToken);
        }

        using var source = OpenCvImageFrameConverter.DecodeFrame(grayscaleImage);
        if (source.Channels() != 1)
        {
            throw new InvalidOperationException("二值化节点要求输入为单通道灰度图，或启用自动灰度化。");
        }

        using var result = new Mat();
        Cv2.Threshold(
            source,
            result,
            threshold,
            maxValue,
            OpenCvImageFrameConverter.MapThresholdMode(mode));

        return OpenCvImageFrameConverter.CreateFrame(
            result,
            grayscaleImage.Format,
            grayscaleImage.SourcePath);
    }

    private static Mat ConvertColor(Mat source, ColorConversionCodes conversionCode)
    {
        var result = new Mat();
        Cv2.CvtColor(source, result, conversionCode);
        return result;
    }
}

internal static class OpenCvImageFrameConverter
{
    public static WorkflowImageFrame CreateFrame(Mat image, string? preferredFormat, string? sourcePath)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Empty())
        {
            throw new InvalidOperationException("无法从空图像创建工作流图片帧。");
        }

        var format = NormalizeFormat(preferredFormat);
        if (!Cv2.ImEncode(format, image, out var content))
        {
            throw new InvalidOperationException("OpenCV 无法对图像进行编码。");
        }

        return new WorkflowImageFrame
        {
            Content = content,
            Format = format,
            Width = image.Width,
            Height = image.Height,
            Channels = image.Channels(),
            IsGrayscale = image.Channels() == 1,
            SourcePath = sourcePath
        };
    }

    public static Mat DecodeFrame(WorkflowImageFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Content.Length == 0)
        {
            throw new InvalidOperationException("图片帧内容为空，无法解码。");
        }

        var mat = Cv2.ImDecode(image.Content, ImreadModes.Unchanged);
        if (mat.Empty())
        {
            mat.Dispose();
            throw new InvalidOperationException("OpenCV 无法解码图片帧内容。");
        }

        return mat;
    }

    public static ImreadModes MapReadMode(WorkflowImageReadMode mode)
    {
        return mode switch
        {
            WorkflowImageReadMode.Unchanged => ImreadModes.Unchanged,
            WorkflowImageReadMode.Color => ImreadModes.Color,
            WorkflowImageReadMode.Grayscale => ImreadModes.Grayscale,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "不支持的图片读取模式。")
        };
    }

    public static ThresholdTypes MapThresholdMode(WorkflowThresholdMode mode)
    {
        return mode switch
        {
            WorkflowThresholdMode.Binary => ThresholdTypes.Binary,
            WorkflowThresholdMode.BinaryInverted => ThresholdTypes.BinaryInv,
            WorkflowThresholdMode.Truncate => ThresholdTypes.Trunc,
            WorkflowThresholdMode.ToZero => ThresholdTypes.Tozero,
            WorkflowThresholdMode.ToZeroInverted => ThresholdTypes.TozeroInv,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "不支持的二值化模式。")
        };
    }

    private static string NormalizeFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return ".png";
        }

        var normalized = format.Trim();
        if (!normalized.StartsWith(".", StringComparison.Ordinal))
        {
            normalized = $".{normalized}";
        }

        return normalized.ToLowerInvariant();
    }
}
