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
            SettingsFields: Fields(("text", "文本内容"))));
        registry.Register("demo.node.csv-read", new CsvReadNode(), new NodeTypeDescriptor(
            "demo.node.csv-read", "CSV读取节点", "Source",
            SettingsFields: Fields(("filePath", "文件路径"))));
        registry.Register("demo.node.uppercase-transform", new UppercaseTransformNode(), new NodeTypeDescriptor(
            "demo.node.uppercase-transform", "转换节点", "Transform"));
        registry.Register("demo.node.csv-to-tsv-transform", new CsvToTsvTransformNode(), new NodeTypeDescriptor(
            "demo.node.csv-to-tsv-transform", "CSV转TSV", "Transform"));
        registry.Register("demo.node.preview", new PreviewNode(), new NodeTypeDescriptor(
            "demo.node.preview", "预览节点", "Output"));
        registry.Register("demo.node.tsv-save", new TsvSaveNode(), new NodeTypeDescriptor(
            "demo.node.tsv-save", "TSV保存", "Output",
            SettingsFields: Fields(("filePath", "文件路径"))));
        registry.Register("opencv.node.image-read", new OpenCvImageReadNode(codec), new NodeTypeDescriptor(
            "opencv.node.image-read", "图片读取", "OpenCV",
            SettingsFields: Fields(("filePath", "文件路径"), ("readMode", "读取模式"))));
        registry.Register("opencv.node.grayscale", new OpenCvGrayscaleNode(processing), new NodeTypeDescriptor(
            "opencv.node.grayscale", "灰度化", "OpenCV"));
        registry.Register("opencv.node.threshold", new OpenCvThresholdNode(processing), new NodeTypeDescriptor(
            "opencv.node.threshold", "二值化", "OpenCV",
            SettingsFields: Fields(("thresholdValue", "阈值"), ("maxValue", "最大值"), ("thresholdMode", "模式"), ("autoConvertToGrayscale", "自动灰度化"))));
        registry.Register("opencv.node.image-save", new OpenCvImageSaveNode(codec), new NodeTypeDescriptor(
            "opencv.node.image-save", "图片保存", "OpenCV",
            SettingsFields: Fields(("filePath", "文件路径"))));
    }

    private static IReadOnlyList<NodeSettingField> Fields(params (string Key, string DisplayName)[] entries)
    {
        return entries.Select(entry => new NodeSettingField(entry.Key, entry.DisplayName)).ToList();
    }
}
