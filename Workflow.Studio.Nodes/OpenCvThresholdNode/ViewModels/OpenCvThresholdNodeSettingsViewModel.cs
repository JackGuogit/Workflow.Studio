using CommunityToolkit.Mvvm.ComponentModel;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Nodes.OpenCvThreshold.ViewModels;

public partial class OpenCvThresholdNodeSettingsViewModel : ObservableObject, INodeSettings
{
    [ObservableProperty]
    private byte _thresholdValue = 127;

    [ObservableProperty]
    private byte _maxValue = 255;

    [ObservableProperty]
    private WorkflowThresholdMode _thresholdMode = WorkflowThresholdMode.Binary;

    [ObservableProperty]
    private bool _autoConvertToGrayscale = true;

    public IReadOnlyList<WorkflowThresholdMode> AvailableThresholdModes { get; } = Enum.GetValues<WorkflowThresholdMode>();

    public string Title => "二值化节点";

    public string Description => "配置 OpenCV 阈值二值化参数。";
}
