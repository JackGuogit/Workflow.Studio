using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Nodes.TextSource.ViewModels;

public partial class TextSourceNodeSettingsViewModel : ObservableObject, INodeSettings
{
    [ObservableProperty]
    private string _text = "Build workflow apps with WPF";

    public string Title => "输入节点";

    public string Description => "配置工作流的初始文本输出。";
}
