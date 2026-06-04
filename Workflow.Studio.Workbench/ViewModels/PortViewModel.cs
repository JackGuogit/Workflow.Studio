using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
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

    public string SemanticBadgeText => GetSemanticBadgeText(Model.Metadata.SemanticTypeKey);

    public string TypeDisplayText => $"{DataTypeName} / {SemanticTypeKey}";

    public Brush SemanticAccentBrush => GetSemanticAccentBrush(Model.Metadata.SemanticTypeKey);

    public Brush SemanticBadgeBackground => GetSemanticBadgeBackground(Model.Metadata.SemanticTypeKey);

    public Brush SemanticBadgeForeground => Brushes.White;

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

    private static string GetSemanticBadgeText(string? semanticTypeKey)
    {
        return semanticTypeKey switch
        {
            WorkflowPortSemanticTypes.PlainText => "TEXT",
            WorkflowPortSemanticTypes.CsvText => "CSV",
            WorkflowPortSemanticTypes.TsvText => "TSV",
            WorkflowPortSemanticTypes.FilePath => "PATH",
            WorkflowPortSemanticTypes.PreviewText => "PREVIEW",
            WorkflowPortSemanticTypes.ImageFrame => "IMG",
            _ => "GEN"
        };
    }

    private static Brush GetSemanticAccentBrush(string? semanticTypeKey)
    {
        return semanticTypeKey switch
        {
            WorkflowPortSemanticTypes.PlainText => CreateBrush(0x60, 0xA5, 0xFA),
            WorkflowPortSemanticTypes.CsvText => CreateBrush(0xF5, 0x9E, 0x0B),
            WorkflowPortSemanticTypes.TsvText => CreateBrush(0x10, 0xB9, 0x81),
            WorkflowPortSemanticTypes.FilePath => CreateBrush(0x8B, 0x5C, 0xF6),
            WorkflowPortSemanticTypes.PreviewText => CreateBrush(0xEC, 0x48, 0x99),
            WorkflowPortSemanticTypes.ImageFrame => CreateBrush(0x14, 0xB8, 0xA6),
            _ => CreateBrush(0x6B, 0x72, 0x80)
        };
    }

    private static Brush GetSemanticBadgeBackground(string? semanticTypeKey)
    {
        return semanticTypeKey switch
        {
            WorkflowPortSemanticTypes.PlainText => CreateBrush(0x25, 0x63, 0xEB),
            WorkflowPortSemanticTypes.CsvText => CreateBrush(0xD9, 0x77, 0x06),
            WorkflowPortSemanticTypes.TsvText => CreateBrush(0x05, 0x96, 0x69),
            WorkflowPortSemanticTypes.FilePath => CreateBrush(0x7C, 0x3A, 0xED),
            WorkflowPortSemanticTypes.PreviewText => CreateBrush(0xDB, 0x27, 0x77),
            WorkflowPortSemanticTypes.ImageFrame => CreateBrush(0x0F, 0x76, 0x66),
            _ => CreateBrush(0x4B, 0x55, 0x63)
        };
    }

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
