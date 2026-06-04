using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Nodes.OpenCvImageSave.ViewModels;

public partial class OpenCvImageSaveNodeSettingsViewModel : ObservableObject, INodeSettings
{
    [ObservableProperty]
    private string _filePath = "output-image.png";

    public string Title => "图片保存节点";

    public string Description => "将工作流图片帧写入指定图片文件。";
}
