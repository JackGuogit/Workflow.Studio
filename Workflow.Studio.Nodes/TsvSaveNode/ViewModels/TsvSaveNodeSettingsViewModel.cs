using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Nodes.TsvSave.ViewModels;

public partial class TsvSaveNodeSettingsViewModel : ObservableObject, INodeSettings
{
    [ObservableProperty]
    private string _filePath = "output-data.tsv";

    public string Title => "TSV保存节点";

    public string Description => "将 TSV 文本写入指定文件。";
}
