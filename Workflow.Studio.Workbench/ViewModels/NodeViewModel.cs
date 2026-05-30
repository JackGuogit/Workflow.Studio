using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class NodeViewModel : ObservableObject
{
    private Point _location;
    private NodeStatus _status;

    public NodeViewModel(NodeData model)
    {
        Model = model;
        _location = new Point(model.Layout.X, model.Layout.Y);
        _status = model.Status;

        InputPorts = new ObservableCollection<PortViewModel>(model.InputPorts.Select(port => new PortViewModel(this, port)));
        OutputPorts = new ObservableCollection<PortViewModel>(model.OutputPorts.Select(port => new PortViewModel(this, port)));
        InputGroups = BuildGroups(InputPorts);
        OutputGroups = BuildGroups(OutputPorts);
    }

    public NodeData Model { get; }

    public ObservableCollection<PortViewModel> InputPorts { get; }

    public ObservableCollection<PortViewModel> OutputPorts { get; }

    public ObservableCollection<PortGroupViewModel> InputGroups { get; }

    public ObservableCollection<PortGroupViewModel> OutputGroups { get; }

    public string Id => Model.Metadata.Id;

    public string Title => Model.Metadata.Name;

    public string Category => Model.Metadata.Category;

    public string Description => Model.Metadata.Description;

    public string NodeTypeText => $"节点类型: {Model.NodeTypeId}";

    public int ParameterCount => Model.Parameters.Count;

    public string ParameterSummary => Model.Parameters.Count == 0
        ? "无参数"
        : string.Join(Environment.NewLine, Model.Parameters.Select(entry => $"{entry.Key}: {FormatParameterValue(entry.Value)}"));

    public Point Location
    {
        get => _location;
        set
        {
            if (SetProperty(ref _location, value))
            {
                Model.Layout.X = value.X;
                Model.Layout.Y = value.Y;
            }
        }
    }

    public NodeStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(PreviewText));
                OnPropertyChanged(nameof(NodeTypeText));
            }
        }
    }

    public string StatusText => Status.ToString();

    public string PortSummary => $"{InputPorts.Count} in / {OutputPorts.Count} out";

    public string PreviewText
    {
        get
        {
            var activePort = OutputPorts.Concat(InputPorts).FirstOrDefault(port => port.Value is not null);
            return activePort is null ? "No data yet" : $"{activePort.Title}: {activePort.DisplayValue}";
        }
    }

    public PortViewModel? FindPort(string portId)
    {
        return InputPorts.Concat(OutputPorts).FirstOrDefault(port => string.Equals(port.Id, portId, StringComparison.OrdinalIgnoreCase));
    }

    public void AttachConnection(ConnectionViewModel connection)
    {
        connection.Source.AttachConnection(connection);
        connection.Target.AttachConnection(connection);
        OnPropertyChanged(nameof(PreviewText));
    }

    public void Refresh()
    {
        Status = Model.Status;

        foreach (var port in InputPorts.Concat(OutputPorts))
        {
            port.Refresh();
        }

        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(PortSummary));
        OnPropertyChanged(nameof(ParameterCount));
        OnPropertyChanged(nameof(ParameterSummary));
    }

    public void UpdateParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        Model.Parameters.Clear();

        foreach (var entry in parameters)
        {
            Model.Parameters[entry.Key] = entry.Value;
        }

        OnPropertyChanged(nameof(ParameterCount));
        OnPropertyChanged(nameof(ParameterSummary));
    }

    private static ObservableCollection<PortGroupViewModel> BuildGroups(IEnumerable<PortViewModel> ports)
    {
        var groups = ports
            .GroupBy(port => port.GroupName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PortGroupViewModel(group.Key, group.OrderBy(port => port.Title, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        return new ObservableCollection<PortGroupViewModel>(groups);
    }

    private static string FormatParameterValue(object? value)
    {
        return value switch
        {
            null => "(null)",
            string text when string.IsNullOrWhiteSpace(text) => "(empty)",
            string text => text,
            IDictionary dictionary => $"对象 ({dictionary.Count})",
            ICollection collection => $"集合 ({collection.Count})",
            _ => value.ToString() ?? string.Empty
        };
    }
}
