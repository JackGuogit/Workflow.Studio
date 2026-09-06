using Workflow.Studio.Core.Plugins;
using Workflow.Studio.Core.Session;

namespace Workflow.Studio.Nodes.BuiltIn;

/// <summary>
/// 内置节点目录：Desktop 与 CLI 共用的装配入口。
/// </summary>
public static class BuiltInNodeCatalog
{
    public static void RegisterAll(
        WorkflowDefinitionRegistry registry,
        IOpenCvImageCodecPlugin codec,
        IOpenCvImageProcessingPlugin processing)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(processing);

        registry.Register("demo.node.text-source", new TextSourceNode(), new NodeTypeDescriptor(
            "demo.node.text-source", "输入节点", "Source",
            SettingsFields: Fields(("text", "文本内容", null))));
        registry.Register("demo.node.csv-read", new CsvReadNode(), new NodeTypeDescriptor(
            "demo.node.csv-read", "CSV读取节点", "Source",
            SettingsFields: Fields(("filePath", "文件路径", null))));
        registry.Register("demo.node.uppercase-transform", new UppercaseTransformNode(), new NodeTypeDescriptor(
            "demo.node.uppercase-transform", "转换节点", "Transform"));
        registry.Register("demo.node.csv-to-tsv-transform", new CsvToTsvTransformNode(), new NodeTypeDescriptor(
            "demo.node.csv-to-tsv-transform", "CSV转TSV", "Transform"));
        registry.Register("demo.node.preview", new PreviewNode(), new NodeTypeDescriptor(
            "demo.node.preview", "预览节点", "Output"));
        registry.Register("demo.node.tsv-save", new TsvSaveNode(), new NodeTypeDescriptor(
            "demo.node.tsv-save", "TSV保存", "Output",
            SettingsFields: Fields(("filePath", "文件路径", null))));
        registry.Register("opencv.node.image-read", new OpenCvImageReadNode(codec), new NodeTypeDescriptor(
            "opencv.node.image-read", "图片读取", "OpenCV",
            SettingsFields: new List<NodeSettingField>
            {
                new("filePath", "文件路径"),
                new("readMode", "读取模式", "enum", new[] { "Unchanged", "Color", "Grayscale" })
            },
            RequiredCapabilities: ["OpenCvImageCodec"]));
        registry.Register("opencv.node.grayscale", new OpenCvGrayscaleNode(processing), new NodeTypeDescriptor(
            "opencv.node.grayscale", "灰度化", "OpenCV",
            RequiredCapabilities: ["OpenCvImageProcessing"]));
        registry.Register("opencv.node.threshold", new OpenCvThresholdNode(processing), new NodeTypeDescriptor(
            "opencv.node.threshold", "二值化", "OpenCV",
            SettingsFields: new List<NodeSettingField>
            {
                new("thresholdValue", "阈值", "number"),
                new("maxValue", "最大值", "number"),
                new("thresholdMode", "模式", "enum", new[] { "Binary", "BinaryInverted", "Truncate", "ToZero", "ToZeroInverted" }),
                new("autoConvertToGrayscale", "自动灰度化", "bool")
            },
            RequiredCapabilities: ["OpenCvImageProcessing"]));
        registry.Register("opencv.node.image-save", new OpenCvImageSaveNode(codec), new NodeTypeDescriptor(
            "opencv.node.image-save", "图片保存", "OpenCV",
            SettingsFields: Fields(("filePath", "文件路径", null)),
            RequiredCapabilities: ["OpenCvImageCodec"]));
    }

    private static IReadOnlyList<NodeSettingField> Fields(params (string Key, string DisplayName, string? EditorKind)[] entries)
    {
        return entries.Select(entry => new NodeSettingField(entry.Key, entry.DisplayName, entry.EditorKind)).ToList();
    }
}
