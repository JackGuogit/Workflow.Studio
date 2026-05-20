using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed partial class PortGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    public PortGroupViewModel(string name, IEnumerable<PortViewModel> ports)
    {
        Name = name;
        Ports = new ObservableCollection<PortViewModel>(ports);
    }

    public string Name { get; }

    public ObservableCollection<PortViewModel> Ports { get; }

    public int PortCount => Ports.Count;

    public string Summary => $"{PortCount} ports";

    partial void OnIsExpandedChanged(bool value)
    {
        foreach (var port in Ports)
        {
            port.SetCollapsed(!value);
        }

        OnPropertyChanged(nameof(Summary));
    }
}
