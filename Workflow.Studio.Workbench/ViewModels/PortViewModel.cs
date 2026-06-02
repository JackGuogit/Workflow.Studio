using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed class PortViewModel : ObservableObject
{
    private Point _anchor;
    private object? _value;
    private PortStatus _status;

    public PortViewModel(NodeViewModel owner, PortData model)
    {
        Owner = owner;
        Model = model;
        _value = model.Value;
        _status = model.Status;
        Connections = [];
    }

    public NodeViewModel Owner { get; }

    public PortData Model { get; }

    public string Id => Model.Metadata.Id;

    public string Title => Model.Metadata.Name;

    public string GroupName => Model.Metadata.GroupName;

    public string DataTypeName => Model.Metadata.DataType.Name;

    public string SemanticTypeKey => string.IsNullOrWhiteSpace(Model.Metadata.SemanticTypeKey)
        ? "未指定"
        : Model.Metadata.SemanticTypeKey;

    public string TypeDisplayText => $"{DataTypeName} / {SemanticTypeKey}";

    public PortDirection Direction => Model.Direction;

    public ObservableCollection<ConnectionViewModel> Connections { get; }

    public Point Anchor
    {
        get => _anchor;
        set => SetProperty(ref _anchor, value);
    }

    public object? Value
    {
        get => _value;
        private set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(DisplayValue));
                OnPropertyChanged(nameof(ToolTipText));
            }
        }
    }

    public PortStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(ToolTipText));
            }
        }
    }

    public bool IsCollapsed => Model.IsCollapsed;

    public bool IsConnected => Status is PortStatus.Connected or PortStatus.HasData;

    public int ConnectionCount => Connections.Count;

    public string StatusText => Status.ToString();

    public string DisplayValue => Value?.ToString() ?? "(empty)";

    public string ToolTipText =>
        $"{Owner.Title}.{Title}{Environment.NewLine}" +
        $"类型: {TypeDisplayText}{Environment.NewLine}" +
        $"状态: {StatusText}{Environment.NewLine}" +
        $"值: {DisplayValue}";

    public void Refresh()
    {
        Value = Model.Value;
        Status = Model.Status;
        OnPropertyChanged(nameof(IsCollapsed));
        OnPropertyChanged(nameof(ConnectionCount));
        OnPropertyChanged(nameof(ToolTipText));
    }

    public void AttachConnection(ConnectionViewModel connection)
    {
        if (!Connections.Contains(connection))
        {
            Connections.Add(connection);
            OnPropertyChanged(nameof(ConnectionCount));
        }
    }

    public void DetachConnection(ConnectionViewModel connection)
    {
        if (Connections.Remove(connection))
        {
            OnPropertyChanged(nameof(ConnectionCount));
        }
    }

    public void SetCollapsed(bool isCollapsed)
    {
        Model.SetCollapsed(isCollapsed);
        OnPropertyChanged(nameof(IsCollapsed));
    }
}
