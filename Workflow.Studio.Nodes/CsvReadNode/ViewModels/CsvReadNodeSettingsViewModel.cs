using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Nodes.CsvRead.ViewModels;

public partial class CsvReadNodeSettingsViewModel : ObservableObject, INodeSettings
{
    [ObservableProperty]
    private string _filePath = "sample-data.csv";

    public string Title => "CSV读取节点";

    public string Description => "从指定文件读取 CSV 文本内容。";
}
