using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Nodes.OpenCvImageRead.ViewModels;

public partial class OpenCvImageReadNodeSettingsViewModel : ObservableObject, INodeSettings
{
    [ObservableProperty]
    private string _filePath = "sample-image.png";

    [ObservableProperty]
    private WorkflowImageReadMode _readMode = WorkflowImageReadMode.Color;

    public IReadOnlyList<WorkflowImageReadMode> AvailableReadModes { get; } = Enum.GetValues<WorkflowImageReadMode>();

    public string Title => "图片读取节点";

    public string Description => "从指定路径读取图片并输出图像帧。";
}
